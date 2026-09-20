using System.Security.Claims;
using Microsoft.OpenApi;
using MiniStore.Common;

namespace MiniStore.Features.Checkout;

public static class CheckoutEndpoints
{
    /// <summary>把本功能的路由注册到应用，并声明访问权限；业务逻辑交给注入的 Service。</summary>
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
            .WithName("QuoteCheckout")
            .WithSummary("计算结算金额并生成报价")
            .WithDescription(
                "需要客户身份。提交本人地址 UUID、配送方式及可选优惠码；金额由服务端计算。报价令牌十分钟有效，不预占库存；优惠券不可用返回 422。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(409)
            .ProducesProblem(422)
            .ProducesProblem(500);

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
            .Produces<OrderCreated>(StatusCodes.Status201Created)
            .Produces<OrderCreated>(StatusCodes.Status200OK)
            .AddOpenApiOperationTransformer(
                (operation, context, ct) =>
                {
                    // 请求头由 HttpContext 读取，文档生成器无法自动发现，因此显式声明。
                    operation.Parameters ??= [];
                    operation.Parameters.Add(
                        new OpenApiParameter
                        {
                            Name = "Idempotency-Key",
                            In = ParameterLocation.Header,
                            Required = true,
                            Description =
                                "本次下单的非空请求键，去除首尾空白后最多 128 字符。新下单换新键；网络重试复用同一键与请求体，建议使用 UUID 字符串。",
                            Schema = new OpenApiSchema
                            {
                                Type = JsonSchemaType.String,
                                MinLength = 1,
                                MaxLength = 128,
                            },
                        }
                    );
                    return Task.CompletedTask;
                }
            )
            .WithName("PlaceOrder")
            .WithSummary("幂等下单并预占库存")
            .WithDescription(
                "需要客户身份及 Idempotency-Key 请求头。用报价令牌与相同地址、配送、优惠创建订单快照，在一个事务中预占单仓库存、核销优惠和转换购物车。首次成功返回 201；相同键和内容重放返回原订单及 200。同键不同内容、报价过期或库存变化返回 409；优惠不可用返回 422。"
            )
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(409)
            .ProducesProblem(422)
            .ProducesProblem(500);
    }
}
