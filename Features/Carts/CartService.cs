using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;

namespace MiniStore.Features.Carts;

/// <summary>处理购物车功能的数据查询与业务规则。同一次请求共用注入的 DbContext，事务边界保留在业务方法内。</summary>
public sealed class CartService(StoreDbContext db)
{
    /// <summary>读取当前活动购物车，以现价计算无优惠券、标准配送的预估合计；空车 Totals 为 null。</summary>
    public async Task<CartDto> GetAsync(long customerId, CancellationToken ct)
    {
        var raw = await db
            .CartItems.AsNoTracking()
            .AsSplitQuery()
            .Include(i => i.Variant)
                .ThenInclude(v => v.Product)
                    .ThenInclude(p => p.Categories)
            .Include(i => i.Variant)
                .ThenInclude(v => v.Stocks)
            .Where(i => i.Cart.CustomerId == customerId && i.Cart.Status == "active")
            .OrderBy(i => i.Id)
            .ToListAsync(ct);
        var prices = raw.Select(i => new MiniStore.Features.Checkout.PriceLine(
                i.Variant.PublicId,
                i.Variant.Sku,
                i.Variant.Product.Name,
                i.Variant.Name,
                i.Quantity,
                i.Variant.Price,
                MiniStore.Features.Products.ProductService.IsFood(i.Variant.Product) ? .08m : .10m
            ))
            .ToList();
        var quote =
            raw.Count > 0
                ? MiniStore.Features.Checkout.Pricing.Calculate(prices, 0, "standard")
                : null;
        var items = raw.Select(i => new CartItemDto(
            i.Variant.PublicId,
            i.Variant.Product.PublicId,
            i.Variant.Sku,
            i.Variant.Product.Name,
            i.Variant.Name,
            i.Variant.Product.Slug,
            i.Quantity,
            i.Variant.Price,
            i.UnitPrice,
            i.Variant.Currency,
            decimal.Round(
                i.Variant.Price
                    * (
                        MiniStore.Features.Products.ProductService.IsFood(i.Variant.Product)
                            ? 1.08m
                            : 1.10m
                    ),
                0,
                MidpointRounding.AwayFromZero
            ),
            quote!.Lines.Single(l => l.VariantId == i.Variant.PublicId).LineTotal,
            i.Variant.Stocks.Sum(s => s.QuantityOnHand - s.QuantityReserved),
            i.Variant.Currency == "JPY"
                && i.Variant.IsActive
                && i.Variant.Product.Status == "active"
                && i.Variant.Product.DeletedAt == null
        ));
        return new CartDto(items, raw.Sum(i => i.Quantity), quote?.Totals);
    }

    /// <summary>把某规格数量设为指定值，必要时建车；只检查库存，不在加购时预占库存。</summary>
    public async Task SetItemAsync(
        Guid id,
        QuantityRequest r,
        long customerId,
        CancellationToken ct
    )
    {
        Rules.Require(r.Quantity is >= 1 and <= 99, "商品数量须为 1～99。");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RowLocks.Customer(db, customerId, ct);
        var variant =
            await db
                .ProductVariants.Include(v => v.Product)
                .SingleOrDefaultAsync(
                    v =>
                        v.PublicId == id
                        && v.IsActive
                        && v.Product.Status == "active"
                        && v.Product.DeletedAt == null,
                    ct
                )
            ?? throw ApiError.NotFound();
        var available = await db
            .Stocks.Where(s => s.VariantId == variant.Id)
            .SumAsync(s => s.QuantityOnHand - s.QuantityReserved, ct);
        // 加购只检查跨仓可售量；库存仍可能被他人购买，Checkout 会再确认单仓能否完整履约。
        if (available < r.Quantity)
            throw ApiError.Conflict("可售库存不足。");
        var cart = await db.Carts.SingleOrDefaultAsync(
            c => c.CustomerId == customerId && c.Status == "active",
            ct
        );
        if (cart == null)
        {
            cart = new()
            {
                CustomerId = customerId,
                Status = "active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.Carts.Add(cart);
        }
        var item =
            // 新建 Cart 尚未保存，数据库还没有为它生成 ID，因此此时不用按 CartId 查询条目。
            cart.Id == 0
                ? null
                : await db.CartItems.SingleOrDefaultAsync(
                    i => i.CartId == cart.Id && i.VariantId == variant.Id,
                    ct
                );
        if (item == null)
        {
            Rules.Require(
                cart.Id == 0 || await db.CartItems.CountAsync(i => i.CartId == cart.Id, ct) < 50,
                "购物车最多 50 种商品。"
            );
            item = new()
            {
                Cart = cart,
                VariantId = variant.Id,
                CreatedAt = DateTime.UtcNow,
            };
            db.CartItems.Add(item);
        }
        item.Quantity = r.Quantity;
        item.UnitPrice = variant.Price;
        item.UpdatedAt = DateTime.UtcNow;
        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>删除本人活动购物车内的规格；不存在时仍视为操作完成，不删除商品或历史订单。</summary>
    public async Task RemoveItemAsync(Guid id, long customerId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RowLocks.Customer(db, customerId, ct);
        await db
            .CartItems.Where(i =>
                i.Cart.CustomerId == customerId
                && i.Cart.Status == "active"
                && i.Variant.PublicId == id
            )
            .ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
    }
}
