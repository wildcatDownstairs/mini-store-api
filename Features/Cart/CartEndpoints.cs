using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Data.Entities;

namespace MiniStore.Features.Cart;

public sealed record QuantityRequest(int Quantity);

public static class CartEndpoints
{
    public static void MapCart(this WebApplication app)
    {
        var group = app.MapGroup("/api/me/cart")
            .RequireAuthorization("Customer")
            .WithTags("购物车");
        group.MapGet(
            "",
            async (ClaimsPrincipal user, StoreDbContext db, CancellationToken ct) =>
            {
                var raw = await db
                    .CartItems.AsNoTracking()
                    .AsSplitQuery()
                    .Include(i => i.Variant)
                        .ThenInclude(v => v.Product)
                            .ThenInclude(p => p.Categories)
                    .Include(i => i.Variant)
                        .ThenInclude(v => v.Stocks)
                    .Where(i => i.Cart.CustomerId == user.ActorId() && i.Cart.Status == "active")
                    .OrderBy(i => i.Id)
                    .ToListAsync(ct);
                var prices = raw.Select(i => new MiniStore.Features.Checkout.PriceLine(
                        i.Variant.PublicId,
                        i.Variant.Sku,
                        i.Variant.Product.Name,
                        i.Variant.Name,
                        i.Quantity,
                        i.Variant.Price,
                        MiniStore.Features.Catalog.CatalogEndpoints.Food(i.Variant.Product)
                            ? .08m
                            : .10m
                    ))
                    .ToList();
                var quote =
                    raw.Count > 0
                        ? MiniStore.Features.Checkout.Pricing.Calculate(prices, 0, "standard")
                        : null;
                var items = raw.Select(i => new
                {
                    variantId = i.Variant.PublicId,
                    productId = i.Variant.Product.PublicId,
                    i.Variant.Sku,
                    productName = i.Variant.Product.Name,
                    variantName = i.Variant.Name,
                    i.Variant.Product.Slug,
                    i.Quantity,
                    unitPrice = i.Variant.Price,
                    previousUnitPrice = i.UnitPrice,
                    i.Variant.Currency,
                    displayPrice = decimal.Round(
                        i.Variant.Price
                            * (
                                MiniStore.Features.Catalog.CatalogEndpoints.Food(i.Variant.Product)
                                    ? 1.08m
                                    : 1.10m
                            ),
                        0,
                        MidpointRounding.AwayFromZero
                    ),
                    lineTotal = quote!
                        .Lines.Single(l => l.VariantId == i.Variant.PublicId)
                        .LineTotal,
                    availableQuantity = i.Variant.Stocks.Sum(s =>
                        s.QuantityOnHand - s.QuantityReserved
                    ),
                    isAvailable = i.Variant.Currency == "JPY"
                        && i.Variant.IsActive
                        && i.Variant.Product.Status == "active"
                        && i.Variant.Product.DeletedAt == null,
                });
                return new
                {
                    items,
                    itemCount = raw.Sum(i => i.Quantity),
                    totals = quote?.Totals,
                };
            }
        );
        group
            .MapPut(
                "/items/{id:guid}",
                async (
                    Guid id,
                    QuantityRequest r,
                    ClaimsPrincipal user,
                    StoreDbContext db,
                    CancellationToken ct
                ) =>
                {
                    Rules.Require(r.Quantity is >= 1 and <= 99, "商品数量须为 1～99。");
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    await RowLocks.Customer(db, user.ActorId(), ct);
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
                    if (available < r.Quantity)
                        throw ApiError.Conflict("可售库存不足。");
                    var cart = await db.Carts.SingleOrDefaultAsync(
                        c => c.CustomerId == user.ActorId() && c.Status == "active",
                        ct
                    );
                    if (cart == null)
                    {
                        cart = new()
                        {
                            CustomerId = user.ActorId(),
                            Status = "active",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                        };
                        db.Carts.Add(cart);
                    }
                    var item =
                        cart.Id == 0
                            ? null
                            : await db.CartItems.SingleOrDefaultAsync(
                                i => i.CartId == cart.Id && i.VariantId == variant.Id,
                                ct
                            );
                    if (item == null)
                    {
                        Rules.Require(
                            cart.Id == 0
                                || await db.CartItems.CountAsync(i => i.CartId == cart.Id, ct) < 50,
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
                    return Results.NoContent();
                }
            )
            .WithSummary("设置购物车商品数量，价格从数据库读取");
        group.MapDelete(
            "/items/{id:guid}",
            async (Guid id, ClaimsPrincipal user, StoreDbContext db, CancellationToken ct) =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                await RowLocks.Customer(db, user.ActorId(), ct);
                await db
                    .CartItems.Where(i =>
                        i.Cart.CustomerId == user.ActorId()
                        && i.Cart.Status == "active"
                        && i.Variant.PublicId == id
                    )
                    .ExecuteDeleteAsync(ct);
                await tx.CommitAsync(ct);
                return Results.NoContent();
            }
        );
    }
}
