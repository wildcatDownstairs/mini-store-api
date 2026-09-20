using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Data.Entities;
using MiniStore.Features.Orders;

namespace MiniStore.Features.Payments;

public sealed record SimulatedPaymentRequest(bool Success);

public sealed record RefundRequest(Guid PaymentId, decimal Amount, string Reason);

public sealed record RefundDecision(bool Approved);

public static class PaymentEndpoints
{
    public static void RequireSimulation(IWebHostEnvironment env, IConfiguration config)
    {
        if (!env.IsDevelopment() || !config.GetValue<bool>("Features:SimulatedPayments"))
            throw new ApiError(403, "simulation_disabled", "当前环境不允许模拟支付或退款结算。");
    }

    public static void MapPayments(this WebApplication app)
    {
        app.MapPost(
                "/api/me/orders/{id:guid}/simulate-payment",
                async (
                    Guid id,
                    SimulatedPaymentRequest r,
                    ClaimsPrincipal user,
                    StoreDbContext db,
                    IWebHostEnvironment env,
                    IConfiguration config,
                    CancellationToken ct
                ) =>
                {
                    RequireSimulation(env, config);
                    return await Simulate(db, id, user.ActorId(), r.Success, ct);
                }
            )
            .RequireAuthorization("Customer")
            .WithTags("模拟支付")
            .WithSummary("仅开发环境：模拟付款结果，不产生真实扣款");
        app.MapPost(
                "/api/admin/orders/{id:guid}/simulate-payment",
                async (
                    Guid id,
                    SimulatedPaymentRequest r,
                    StoreDbContext db,
                    IWebHostEnvironment env,
                    IConfiguration config,
                    CancellationToken ct
                ) =>
                {
                    RequireSimulation(env, config);
                    return await Simulate(db, id, null, r.Success, ct);
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithTags("模拟支付");
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization("AdminRead")
            .WithTags("支付与退款");
        admin.MapGet(
            "/payments",
            async (StoreDbContext db, [AsParameters] ListQuery q, CancellationToken ct) =>
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
                    .Select(p => new
                    {
                        id = p.PublicId,
                        orderId = p.Order.PublicId,
                        p.Order.OrderNumber,
                        p.Provider,
                        p.ProviderTransactionId,
                        p.Method,
                        p.Status,
                        p.Amount,
                        p.Currency,
                        p.CapturedAt,
                        p.CreatedAt,
                        refunded = p.Refunds.Where(r => r.Status == "completed").Sum(r => r.Amount),
                        pendingRefund = p
                            .Refunds.Where(r => r.Status == "pending")
                            .Sum(r => r.Amount),
                        availableRefund = p.CapturedAt != null
                            ? p.Amount
                                - p.Refunds.Where(r =>
                                        r.Status == "completed" || r.Status == "pending"
                                    )
                                    .Sum(r => r.Amount)
                            : 0,
                    })
                    .PageAsync(q, ct);
            }
        );
        admin.MapGet(
            "/refunds",
            async (StoreDbContext db, [AsParameters] ListQuery q, CancellationToken ct) =>
            {
                q.Validate();
                var source = db.Refunds.AsNoTracking();
                if (q.Status != null)
                    source = source.Where(r => r.Status == q.Status);
                if (q.Q is { Length: > 0 })
                {
                    var pattern = "%" + q.Q.Trim() + "%";
                    source = source.Where(r =>
                        EF.Functions.ILike(r.Payment.Order.OrderNumber, pattern)
                    );
                }
                return await source
                    .OrderByDescending(r => r.CreatedAt)
                    .ThenByDescending(r => r.Id)
                    .Select(r => new
                    {
                        id = r.PublicId,
                        paymentId = r.Payment.PublicId,
                        orderId = r.Payment.Order.PublicId,
                        r.Payment.Order.OrderNumber,
                        r.Amount,
                        r.Payment.Currency,
                        r.Reason,
                        r.Status,
                        r.CreatedAt,
                        r.CompletedAt,
                    })
                    .PageAsync(q, ct);
            }
        );
        admin
            .MapPost(
                "/refunds",
                async (RefundRequest r, StoreDbContext db, CancellationToken ct) =>
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
                            x.PaymentId == payment.Id
                            && (x.Status == "pending" || x.Status == "completed")
                        )
                        .SumAsync(x => x.Amount, ct);
                    if (payment.CapturedAt == null || r.Amount > payment.Amount - reserved)
                        throw ApiError.Conflict(
                            "退款超过可退金额（包括待审核申请），或支付尚未成功。"
                        );
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
                    return Results.Ok(new { id = refund.PublicId, refund.Status });
                }
            )
            .RequireAuthorization("AdminWrite");
        admin
            .MapPost(
                "/refunds/{id:guid}/review",
                async (
                    Guid id,
                    RefundDecision r,
                    StoreDbContext db,
                    IWebHostEnvironment env,
                    IConfiguration config,
                    CancellationToken ct
                ) =>
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
                        .Payments.FromSql(
                            $"SELECT * FROM payment.payments WHERE id={paymentId} FOR UPDATE"
                        )
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
                        payment.Status =
                            refunded == payment.Amount ? "refunded" : "partially_refunded";
                        await db.SaveChangesAsync(ct);
                    }
                    // 退款不等于退货入库，这里不改变库存，也不修改已送达订单的历史金额。
                    await tx.CommitAsync(ct);
                    return Results.NoContent();
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithSummary("审核退款；批准只在开发模拟模式可用");
    }

    private static async Task<IResult> Simulate(
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
            return Results.Ok(new { order.Status, alreadyPaid = true });
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
        return Results.Ok(new { order.Status, success });
    }
}
