using System.Security.Claims;
using MiniStore.Common;

namespace MiniStore.Features.Customers;

/// <summary>注册路由和权限，读取请求参数并调用服务；业务规则见 CustomerService。</summary>
public static class CustomerEndpoints
{
    /// <summary>把本功能的路由注册到应用，并声明访问权限；业务逻辑交给注入的 Service。</summary>
    public static void MapCustomers(this WebApplication app)
    {
        var me = app.MapGroup("/api/me").RequireAuthorization("Customer").WithTags("客户与地址");
        me.MapGet(
                "",
                (ClaimsPrincipal user, CustomerService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.GetProfileAsync(user.ActorId(), ct))
            )
            .WithName("GetMyProfile")
            .WithSummary("读取我的客户资料")
            .WithDescription(
                "需要客户身份。内部客户标识由认证上下文提供，不接受查询其他客户；响应不包含密码哈希。"
            )
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(500);

        me.MapPut(
                "",
                async (
                    ProfileRequest r,
                    ClaimsPrincipal user,
                    CustomerService service,
                    CancellationToken ct
                ) =>
                {
                    await service.UpdateProfileAsync(r, user.ActorId(), ct);
                    return TypedResults.Ok(ApiResponse.Ok());
                }
            )
            .WithName("UpdateMyProfile")
            .WithSummary("更新我的客户资料")
            .WithDescription(
                "需要客户身份。更新姓名、电话及可空生日，不修改登录邮箱或账号状态；保存成功返回 200，data 为 null。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(409)
            .Produces<ApiResponse<object?>>(500);

        me.MapGet(
                "/addresses",
                (ClaimsPrincipal user, CustomerService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.ListAddressesAsync(user.ActorId(), ct))
            )
            .WithName("ListMyAddresses")
            .WithSummary("列出我的地址")
            .WithDescription(
                "需要客户身份。只返回本人地址，默认地址优先；地址 UUID 可用于报价与下单。"
            )
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(500);

        me.MapPost(
                "/addresses",
                (
                    AddressRequest r,
                    ClaimsPrincipal user,
                    CustomerService service,
                    CancellationToken ct
                ) => ApiResponse.OkAsync(service.CreateAddressAsync(r, user.ActorId(), ct))
            )
            .WithName("CreateMyAddress")
            .WithSummary("新增我的地址")
            .WithDescription(
                "需要客户身份。支持日本 shipping/billing 地址，每个客户最多 20 条；首次同类型地址自动设为默认，指定默认时原子切换。成功返回地址对象及 200。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(409)
            .Produces<ApiResponse<object?>>(500);

        me.MapPut(
                "/addresses/{id:guid}",
                (
                    Guid id,
                    AddressRequest r,
                    ClaimsPrincipal user,
                    CustomerService service,
                    CancellationToken ct
                ) => ApiResponse.OkAsync(service.UpdateAddressAsync(id, r, user.ActorId(), ct))
            )
            .WithName("UpdateMyAddress")
            .WithSummary("更新我的地址")
            .WithDescription(
                "需要客户身份。id 是地址公开 UUID；仅可编辑本人地址，默认地址按类型保持唯一。地址变更不会改写历史订单的地址快照。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(409)
            .Produces<ApiResponse<object?>>(500);

        me.MapDelete(
                "/addresses/{id:guid}",
                async (
                    Guid id,
                    ClaimsPrincipal user,
                    CustomerService service,
                    CancellationToken ct
                ) =>
                {
                    await service.DeleteAddressAsync(id, user.ActorId(), ct);
                    return TypedResults.Ok(ApiResponse.Ok());
                }
            )
            .WithName("DeleteMyAddress")
            .WithSummary("删除我的地址")
            .WithDescription(
                "需要客户身份。仅可删除本人地址；删除默认地址后选择同类型其他地址作为默认。历史订单保留原地址快照，成功返回 200，data 为 null。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(409)
            .Produces<ApiResponse<object?>>(500);
        var admin = app.MapGroup("/api/admin/customers")
            .RequireAuthorization("AdminRead")
            .WithTags("客户管理");

        admin
            .MapGet(
                "",
                ([AsParameters] ListQuery q, CustomerService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.ListAsync(q, ct))
            )
            .WithName("ListCustomers")
            .WithSummary("分页查询客户")
            .WithDescription(
                "需要 operator 或 viewer。支持 page、pageSize、q、status；q 搜索姓名或邮箱，按注册时间倒序。响应不包含密码哈希。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(500);

        admin
            .MapGet(
                "/{id:guid}",
                (Guid id, CustomerService service, CancellationToken ct) =>
                    ApiResponse.OkAsync(service.GetAsync(id, ct))
            )
            .WithName("GetCustomer")
            .WithSummary("读取客户详情与近期订单")
            .WithDescription(
                "需要 operator 或 viewer。id 是客户公开 UUID；返回资料、地址与最近订单。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(500);

        admin
            .MapPatch(
                "/{id:guid}/status",
                async (
                    Guid id,
                    CustomerStatusRequest r,
                    CustomerService service,
                    CancellationToken ct
                ) =>
                {
                    await service.SetStatusAsync(id, r, ct);
                    return TypedResults.Ok(ApiResponse.Ok());
                }
            )
            .RequireAuthorization("AdminWrite")
            .WithName("SetCustomerStatus")
            .WithSummary("启用或停用客户")
            .WithDescription(
                "仅 operator。状态只接受 active 或 disabled；后续认证会读取数据库当前状态，停用客户的旧令牌不能继续访问受保护接口。"
            )
            .Produces<ApiResponse<object?>>(400)
            .Produces<ApiResponse<object?>>(401)
            .Produces<ApiResponse<object?>>(403)
            .Produces<ApiResponse<object?>>(404)
            .Produces<ApiResponse<object?>>(409)
            .Produces<ApiResponse<object?>>(500);
    }
}
