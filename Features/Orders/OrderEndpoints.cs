using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;

namespace MiniStore.Features.Orders;

public static class OrderEndpoints
{
    public static void MapOrders(this WebApplication app)
    {
        var me = app.MapGroup("/api/me/orders")
            .RequireAuthorization("Customer")
            .WithTags("客户订单");
        var admin = app.MapGroup("/api/admin/orders")
            .RequireAuthorization("AdminRead")
            .WithTags("订单管理");
        me.MapGet(
            "",
            (
                ClaimsPrincipal user,
                StoreDbContext db,
                [AsParameters] ListQuery q,
                CancellationToken ct
            ) => List(db, q, user.ActorId(), ct)
        );
        admin.MapGet(
            "",
            (StoreDbContext db, [AsParameters] ListQuery q, CancellationToken ct) =>
                List(db, q, null, ct)
        );
        me.MapGet(
            "/{id:guid}",
            (Guid id, ClaimsPrincipal user, OrderService service, CancellationToken ct) =>
                service.DetailAsync(id, user.ActorId(), ct)
        );
        admin.MapGet(
            "/{id:guid}",
            (Guid id, OrderService service, CancellationToken ct) =>
                service.DetailAsync(id, null, ct)
        );
        me.MapPost(
            "/{id:guid}/cancel",
            async (Guid id, ClaimsPrincipal user, OrderService service, CancellationToken ct) =>
            {
                await service.CancelAsync(id, user.ActorId(), ct);
                return Results.NoContent();
            }
        );
        admin
            .MapPost(
                "/{id:guid}/cancel",
                async (Guid id, OrderService service, CancellationToken ct) =>
                {
                    await service.CancelAsync(id, null, ct);
                    return Results.NoContent();
                }
            )
            .RequireAuthorization("AdminWrite");
        admin
            .MapPost(
                "/{id:guid}/confirm",
                (Guid id, StoreDbContext db, CancellationToken ct) =>
                    Change(db, id, "pending", "confirmed", ct)
            )
            .RequireAuthorization("AdminWrite");
        admin
            .MapPost(
                "/{id:guid}/process",
                (Guid id, StoreDbContext db, CancellationToken ct) =>
                    Change(db, id, "paid", "processing", ct)
            )
            .RequireAuthorization("AdminWrite");
    }

    private static async Task<IResult> Change(
        StoreDbContext db,
        Guid id,
        string from,
        string to,
        CancellationToken ct
    )
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await RowLocks.Order(db, id, null, ct);
        if (order.Status != from)
            throw ApiError.Conflict("订单状态不允许此操作。");
        OrderService.Transition(order, to, "运营人员处理订单");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.NoContent();
    }

    private static async Task<object> List(
        StoreDbContext db,
        ListQuery q,
        long? customer,
        CancellationToken ct
    )
    {
        q.Validate();
        var orders = db.Orders.AsNoTracking();
        if (customer.HasValue)
            orders = orders.Where(o => o.CustomerId == customer);
        if (q.Status is { Length: > 0 })
            orders = orders.Where(o => o.Status == q.Status);
        if (q.Q is { Length: > 0 })
        {
            var pattern = "%" + q.Q.Trim() + "%";
            orders = orders.Where(o =>
                EF.Functions.ILike(o.OrderNumber, pattern)
                || EF.Functions.ILike(o.Customer.Email, pattern)
            );
        }
        return await orders
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Select(o => new
            {
                id = o.PublicId,
                o.OrderNumber,
                o.Status,
                o.Currency,
                o.GrandTotal,
                o.PlacedAt,
                o.PaidAt,
                customer = o.Customer.LastName + " " + o.Customer.FirstName,
                itemCount = o.OrderItems.Sum(i => i.Quantity),
                firstProductName = o
                    .OrderItems.OrderBy(i => i.Id)
                    .Select(i => i.ProductName)
                    .FirstOrDefault(),
                paymentStatus = o
                    .Payments.OrderByDescending(p => p.Id)
                    .Select(p => p.Status)
                    .FirstOrDefault(),
                shipmentStatus = o
                    .Shipments.OrderByDescending(s => s.Id)
                    .Select(s => s.Status)
                    .FirstOrDefault(),
            })
            .PageAsync(q, ct);
    }
}
