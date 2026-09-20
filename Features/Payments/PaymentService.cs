using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Features.Orders;

namespace MiniStore.Features.Payments;

/// <summary>处理支付与退款功能的数据查询与业务规则。同一次请求共用注入的 DbContext，事务边界保留在业务方法内。</summary>
public sealed class PaymentService(
    StoreDbContext db,
    IWebHostEnvironment env,
    IConfiguration config
)
{
    /// <summary>开发环境模拟当前客户订单的付款结果；订单归属由内部客户 ID 校验。</summary>
    public async Task<PaymentSimulationDto> SimulateCustomerAsync(
        Guid id,
        SimulatedPaymentRequest r,
        long customerId,
        CancellationToken ct
    )
    {
        RequireSimulation(env, config);
        return await Simulate(db, id, customerId, r.Success, ct);
    }

    /// <summary>开发环境供可写后台模拟付款；该入口没有客户归属限制，必须由端点先完成授权。</summary>
    public async Task<PaymentSimulationDto> SimulateAdminAsync(
        Guid id,
        SimulatedPaymentRequest r,
        CancellationToken ct
    )
    {
        RequireSimulation(env, config);
        return await Simulate(db, id, null, r.Success, ct);
    }

    /// <summary>分页查询支付记录，同时汇总已完成、待审核和剩余可申请退款金额。</summary>
    public async Task<PageResult<PaymentSummaryDto>> ListAsync(ListQuery q, CancellationToken ct)
    {
        q.Validate();
        var source = db.Payments.AsNoTracking();
        if (q.Status != null)
            source = source.Where(p => p.Status == q.Status);
        if (q.Q is { Length: > 0 })
        {
            var pattern = "%" + q.Q.Trim() + "%";
            source = source.Where(p =>
                EF.Functions.ILike(p.Order.OrderNumber, pattern)
                || p.ProviderTransactionId != null
                    && EF.Functions.ILike(p.ProviderTransactionId, pattern)
            );
        }
        return await source
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Select(p => new PaymentSummaryDto(
                p.PublicId,
                p.Order.PublicId,
                p.Order.OrderNumber,
                p.Provider,
                p.ProviderTransactionId,
                p.Method,
                p.Status,
                p.Amount,
                p.Currency,
                p.CapturedAt,
                p.CreatedAt,
                p.Refunds.Where(r => r.Status == "completed").Sum(r => r.Amount),
                p.Refunds.Where(r => r.Status == "pending").Sum(r => r.Amount),
                p.CapturedAt != null
                    ? p.Amount
                        - p.Refunds.Where(r => r.Status == "completed" || r.Status == "pending")
                            .Sum(r => r.Amount)
                    : 0
            ))
            .PageAsync(q, ct);
    }

    /// <summary>分页查询退款申请及所属订单；金额与原支付同币种。</summary>
    public async Task<PageResult<RefundSummaryDto>> ListRefundsAsync(
        ListQuery q,
        CancellationToken ct
    )
    {
        q.Validate();
        var source = db.Refunds.AsNoTracking();
        if (q.Status != null)
            source = source.Where(r => r.Status == q.Status);
        if (q.Q is { Length: > 0 })
        {
            var pattern = "%" + q.Q.Trim() + "%";
            source = source.Where(r => EF.Functions.ILike(r.Payment.Order.OrderNumber, pattern));
        }
        return await source
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Select(r => new RefundSummaryDto(
                r.PublicId,
                r.Payment.PublicId,
                r.Payment.Order.PublicId,
                r.Payment.Order.OrderNumber,
                r.Amount,
                r.Payment.Currency,
                r.Reason,
                r.Status,
                r.CreatedAt,
                r.CompletedAt
            ))
            .PageAsync(q, ct);
    }

    /// <summary>锁定支付行，计入待审核与已完成退款后校验额度，再创建 pending 申请。</summary>
    public async Task<RefundCreatedDto> RequestRefundAsync(RefundRequest r, CancellationToken ct)
    {
        Rules.Money(r.Amount, "退款金额", true);
        var reason = Rules.Text(r.Reason, 500, "退款原因");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var payment =
            await db
                .Payments.FromSql(
                    $"SELECT * FROM payment.payments WHERE public_id={r.PaymentId} FOR UPDATE"
                )
                .SingleOrDefaultAsync(ct)
            ?? throw ApiError.NotFound();
        var reserved = await db
            .Refunds.Where(x =>
                x.PaymentId == payment.Id && (x.Status == "pending" || x.Status == "completed")
            )
            .SumAsync(x => x.Amount, ct);
        if (payment.CapturedAt == null || r.Amount > payment.Amount - reserved)
            throw ApiError.Conflict("退款超过可退金额（包括待审核申请），或支付尚未成功。");
        var refund = new Refund
        {
            PaymentId = payment.Id,
            Amount = r.Amount,
            Reason = reason,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
        };
        db.Refunds.Add(refund);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new RefundCreatedDto(refund.PublicId, refund.Status);
    }

    /// <summary>处理尚未审核的退款；批准时模拟完成退款并更新支付状态，拒绝时标记 failed。</summary>
    public async Task ReviewRefundAsync(Guid id, RefundDecision r, CancellationToken ct)
    {
        if (r.Approved)
            RequireSimulation(env, config);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var paymentId =
            await db
                .Refunds.Where(x => x.PublicId == id)
                .Select(x => (long?)x.PaymentId)
                .SingleOrDefaultAsync(ct)
            ?? throw ApiError.NotFound();
        var payment = await db
            .Payments.FromSql($"SELECT * FROM payment.payments WHERE id={paymentId} FOR UPDATE")
            .SingleAsync(ct);
        var refund = await db.Refunds.SingleAsync(x => x.PublicId == id, ct);
        if (refund.Status != "pending")
            throw ApiError.Conflict("退款申请已经处理。");
        refund.Status = r.Approved ? "completed" : "failed";
        if (r.Approved)
        {
            refund.CompletedAt = DateTime.UtcNow;
            refund.ProviderRefundId = "SIM-REFUND-" + Guid.NewGuid().ToString("N");
        }
        await db.SaveChangesAsync(ct);
        if (r.Approved)
        {
            var refunded = await db
                .Refunds.Where(x => x.PaymentId == paymentId && x.Status == "completed")
                .SumAsync(x => x.Amount, ct);
            payment.Status = refunded == payment.Amount ? "refunded" : "partially_refunded";
            await db.SaveChangesAsync(ct);
        }
        // 退款不等于退货入库，这里不改变库存，也不修改已送达订单的历史金额。
        await tx.CommitAsync(ct);
    }

    /// <summary>只有 Development 且显式开启模拟支付开关时允许模拟资金结算。</summary>
    private static void RequireSimulation(IWebHostEnvironment env, IConfiguration config)
    {
        if (!env.IsDevelopment() || !config.GetValue<bool>("Features:SimulatedPayments"))
            throw new ApiError(403, "simulation_disabled", "当前环境不允许模拟支付或退款结算。");
    }

    /// <summary>锁定订单并记录一次支付尝试；已付款订单再次成功调用仅返回重放结果，不重复创建收款。</summary>
    private static async Task<PaymentSimulationDto> Simulate(
        StoreDbContext db,
        Guid id,
        long? customer,
        bool success,
        CancellationToken ct
    )
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await RowLocks.Order(db, id, customer, ct);
        if (order.PaidAt != null && success)
            return new PaymentSimulationDto(order.Status, AlreadyPaid: true);
        if (order.Status is not ("pending" or "confirmed") || order.PaidAt != null)
            throw ApiError.Conflict("该订单不能付款。");
        if (order.Status == "pending")
            OrderService.Transition(order, "confirmed", "模拟支付前确认订单");
        var now = DateTime.UtcNow;
        db.Payments.Add(
            new Payment
            {
                OrderId = order.Id,
                Provider = "card_gateway",
                ProviderTransactionId = "SIM-" + Guid.NewGuid().ToString("N"),
                Method = "credit_card",
                Status = success ? "captured" : "failed",
                Amount = order.GrandTotal,
                Currency = order.Currency,
                CreatedAt = now,
                AuthorizedAt = success ? now : null,
                CapturedAt = success ? now : null,
                FailedAt = success ? null : now,
            }
        );
        if (success)
            OrderService.Transition(order, "paid", "开发环境模拟支付成功");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new PaymentSimulationDto(order.Status, Success: success);
    }
}
