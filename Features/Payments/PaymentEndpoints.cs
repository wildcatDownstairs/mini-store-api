using System.Security.Claims;
using MiniStore.Common;

namespace MiniStore.Features.Payments;

/// <summary>注册路由和权限，读取请求参数并调用服务；业务规则见 PaymentService。</summary>
public static class PaymentEndpoints
{
    /// <summary>把本功能的路由注册到应用，并声明访问权限；业务逻辑交给注入的 Service。</summary>
    public static void MapPayments(this WebApplication app)
    {
        app.MapPost(
                "/api/me/orders/{id:guid}/simulate-payment",
                (
                    Guid id,
                    SimulatedPaymentRequest r,
                    ClaimsPrincipal user,
                    PaymentService service,
                    CancellationToken ct
                ) => service.SimulateCustomerAsync(id, r, user.ActorId(), ct)
            )
            .RequireAuthorization("Customer")
            .WithTags("模拟支付")
            .WithName("SimulateMyPayment")
            .WithSummary("模拟我的订单付款")
            .WithDescription(
                "需要客户身份，且仅 Development 开启模拟支付时可用。id 是本人订单 UUID；success=false 记录失败尝试，true 记录收款。已付款订单再次成功调用不重复扣款；不会调用真实网关。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(500);
        app.MapPost(
                "/api/admin/orders/{id:guid}/simulate-payment",
                (
                    Guid id,
                    SimulatedPaymentRequest r,
                    PaymentService service,
                    CancellationToken ct
                ) => service.SimulateAdminAsync(id, r, ct)
            )
            .RequireAuthorization("AdminWrite")
            .WithTags("模拟支付")
            .WithName("SimulateAdminPayment")
            .WithSummary("后台模拟订单付款")
            .WithDescription(
                "仅 operator，且仅 Development 开启模拟支付时可用。记录订单金额的模拟付款结果，已成功付款可安全重放；环境不允许时返回 403，不产生真实资金流。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(500);
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization("AdminRead")
            .WithTags("支付与退款");
        admin
            .MapGet(
                "/payments",
                ([AsParameters] ListQuery q, PaymentService service, CancellationToken ct) =>
                    service.ListAsync(q, ct)
            )
            .WithName("ListPayments")
            .WithSummary("分页查询支付及可退金额")
            .WithDescription(
                "需要 operator 或 viewer。支持 page、pageSize、q、status；q 搜索订单号或交易号。可退金额扣除已完成与待审核退款，未成功收款的可退额为零。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(500);
        admin
            .MapGet(
                "/refunds",
                ([AsParameters] ListQuery q, PaymentService service, CancellationToken ct) =>
                    service.ListRefundsAsync(q, ct)
            )
            .WithName("ListRefunds")
            .WithSummary("分页查询退款申请")
            .WithDescription(
                "需要 operator 或 viewer。支持 page、pageSize、q、status；q 搜索订单号。返回原支付 UUID、订单与申请金额，按申请时间倒序。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(500);
        admin
            .MapPost(
                "/refunds",
                (RefundRequest r, PaymentService service, CancellationToken ct) =>
                    service.RequestRefundAsync(r, ct)
            )
            .RequireAuthorization("AdminWrite")
            .WithName("RequestRefund")
            .WithSummary("申请订单支付退款")
            .WithDescription(
                "仅 operator。paymentId 为支付公开 UUID，amount 为正整数 JPY。锁定支付记录，将待审核与已完成退款合计后核对可退余额；成功创建 pending 申请并返回 200。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(500);
        admin
            .MapPost(
                "/refunds/{id:guid}/review",
                async (Guid id, RefundDecision r, PaymentService service, CancellationToken ct) =>
                {
                    await service.ReviewRefundAsync(id, r, ct);
                    return TypedResults.NoContent();
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithName("ReviewRefund")
            .WithSummary("审核退款申请")
            .WithDescription(
                "仅 operator。id 是退款公开 UUID；只处理 pending，重复审核返回 409。approved=true 仅开发模拟模式可用并完成退款；拒绝标记 failed。退款不会自动退货入库或改写订单成交金额。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(500);
    }
}
