using Microsoft.EntityFrameworkCore;
using MiniStore.Data;
using MiniStore.Features.Customers;
using MiniStore.Features.Orders;

namespace MiniStore.Common;

/// <summary>必须在事务内调用。固定先客户、再订单/优惠券、再库存的加锁顺序，减少并发冲突。</summary>
public static class RowLocks
{
    public static Task<Customer> Customer(StoreDbContext db, long id, CancellationToken ct) =>
        db
            .Customers.FromSql($"SELECT * FROM account.customers WHERE id = {id} FOR UPDATE")
            .SingleAsync(ct);

    public static async Task<Order> Order(
        StoreDbContext db,
        Guid id,
        long? customer,
        CancellationToken ct
    )
    {
        var order = await db
            .Orders.FromSql($"SELECT * FROM sales.orders WHERE public_id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (order is null || customer.HasValue && order.CustomerId != customer.Value)
            throw ApiError.NotFound();
        return order;
    }
}
