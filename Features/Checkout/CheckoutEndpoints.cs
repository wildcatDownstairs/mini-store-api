using System.Security.Claims;
using MiniStore.Common;

namespace MiniStore.Features.Checkout;

public static class CheckoutEndpoints
{
    public static void MapCheckout(this WebApplication app)
    {
        app.MapPost(
                "/api/me/checkout/quote",
                (
                    QuoteRequest r,
                    ClaimsPrincipal user,
                    CheckoutService checkout,
                    CancellationToken ct
                ) => checkout.QuoteAsync(user.ActorId(), r, ct)
            )
            .RequireAuthorization("Customer")
            .WithTags("结算")
            .WithSummary("服务端计算价格并签发十分钟有效报价");
        app.MapPost(
                "/api/me/orders",
                async (
                    PlaceOrderRequest r,
                    HttpContext http,
                    CheckoutService checkout,
                    CancellationToken ct
                ) =>
                {
                    var order = await checkout.PlaceAsync(
                        http.User.ActorId(),
                        http.Request.Headers["Idempotency-Key"].ToString(),
                        r,
                        ct
                    );
                    return Results.Json(order, statusCode: order.Replayed ? 200 : 201);
                }
            )
            .RequireAuthorization("Customer")
            .WithTags("结算")
            .WithSummary("幂等创建订单、地址与商品快照并预占库存");
    }
}
