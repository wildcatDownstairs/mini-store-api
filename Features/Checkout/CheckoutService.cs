using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Data.Entities;
using MiniStore.Features.Catalog;
using MiniStore.Features.Orders;

namespace MiniStore.Features.Checkout;

public sealed record QuoteRequest(Guid AddressId, string ShippingMethod, string? CouponCode);

public sealed record PlaceOrderRequest(
    Guid AddressId,
    string ShippingMethod,
    string? CouponCode,
    string QuoteToken
);

public sealed record QuoteResponse(
    string QuoteToken,
    DateTime ExpiresAt,
    IReadOnlyList<QuotedLine> Lines,
    Totals Totals
);

public sealed record OrderCreated(
    Guid Id,
    string OrderNumber,
    string Status,
    decimal GrandTotal,
    bool Replayed
);

internal sealed record QuoteSignature(long CustomerId, string Digest);

internal sealed record CheckoutData(
    MiniStore.Data.Entities.Cart Cart,
    CustomerAddress Address,
    Coupon? Coupon,
    QuoteCore Quote
);

/// <summary>一个作用域一个 DbContext。构造参数由 ASP.NET Core 的依赖注入容器提供，不能把此服务注册成单例。</summary>
public sealed class CheckoutService(StoreDbContext db, IDataProtectionProvider protection)
{
    private readonly ITimeLimitedDataProtector protector = protection
        .CreateProtector("MiniStore.Checkout.v1")
        .ToTimeLimitedDataProtector();

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string Digest(CheckoutData d, string shipping) =>
        Hash(
            JsonSerializer.Serialize(
                new
                {
                    d.Quote,
                    shipping,
                    coupon = d.Coupon?.Code,
                    d.Address.PublicId,
                    d.Address.RecipientName,
                    d.Address.PostalCode,
                    d.Address.Prefecture,
                    d.Address.City,
                    d.Address.AddressLine1,
                    d.Address.AddressLine2,
                    d.Address.Phone,
                }
            )
        );

    public async Task<QuoteResponse> QuoteAsync(
        long customer,
        QuoteRequest request,
        CancellationToken ct
    )
    {
        var data = await ReadAsync(customer, request, ct);
        var token = protector.Protect(
            JsonSerializer.Serialize(
                new QuoteSignature(customer, Digest(data, request.ShippingMethod))
            ),
            TimeSpan.FromMinutes(10)
        );
        return new(token, DateTime.UtcNow.AddMinutes(10), data.Quote.Lines, data.Quote.Totals);
    }

    public async Task<OrderCreated> PlaceAsync(
        long customer,
        string key,
        PlaceOrderRequest request,
        CancellationToken ct
    )
    {
        key = Rules.Text(key, 128, "Idempotency-Key");
        Rules.Require(
            !string.IsNullOrWhiteSpace(request.QuoteToken) && request.QuoteToken.Length < 20000,
            "请先获取有效报价。"
        );
        var requestHash = Hash(JsonSerializer.Serialize(request));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var owner = await RowLocks.Customer(db, customer, ct);
        if (owner.Status != "active" || owner.DeletedAt != null)
            throw new ApiError(403, "inactive_customer", "账户已停用。");
        // 先检查已完成请求：即使购物车已转换、报价已过期，重试仍返回原订单。
        var previous = await db
            .CheckoutRequests.Include(r => r.Order)
            .SingleOrDefaultAsync(r => r.CustomerId == customer && r.RequestKey == key, ct);
        if (previous != null)
        {
            if (previous.RequestHash != requestHash)
                throw ApiError.Conflict("该幂等键已用于不同的下单内容。");
            return new(
                previous.Order.PublicId,
                previous.Order.OrderNumber,
                previous.Order.Status,
                previous.Order.GrandTotal,
                true
            );
        }
        QuoteSignature signed;
        try
        {
            signed = JsonSerializer.Deserialize<QuoteSignature>(
                protector.Unprotect(request.QuoteToken)
            )!;
        }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException)
        {
            throw ApiError.Conflict("报价失效或被修改，请重新确认订单。");
        }
        if (signed?.CustomerId != customer)
            throw ApiError.Conflict("报价不属于当前客户。");
        var ids = await db
            .CartItems.Where(i => i.Cart.CustomerId == customer && i.Cart.Status == "active")
            .OrderBy(i => i.VariantId)
            .Select(i => i.VariantId)
            .ToArrayAsync(ct);
        Rules.Require(ids.Length > 0, "购物车为空。");
        var productIds = await db
            .ProductVariants.Where(v => ids.Contains(v.Id))
            .Select(v => v.ProductId)
            .Distinct()
            .OrderBy(x => x)
            .ToArrayAsync(ct);
        await db
            .Products.FromSql(
                $"SELECT *, xmin FROM catalog.products WHERE id = ANY({productIds}) ORDER BY id FOR UPDATE"
            )
            .ToListAsync(ct);
        await db
            .ProductVariants.FromSql(
                $"SELECT * FROM catalog.product_variants WHERE id = ANY({ids}) ORDER BY id FOR UPDATE"
            )
            .ToListAsync(ct);
        var couponCode = request.CouponCode?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(couponCode))
            await db
                .Coupons.FromSql(
                    $"SELECT * FROM marketing.coupons WHERE code = {couponCode} FOR UPDATE"
                )
                .ToListAsync(ct);
        var data = await ReadAsync(
            customer,
            new(request.AddressId, request.ShippingMethod, couponCode),
            ct
        );
        if (signed.Digest != Digest(data, request.ShippingMethod))
            throw ApiError.Conflict("商品价格、购物车或地址已变化，请重新获取报价。");
        var needed = data.Cart.CartItems.ToDictionary(i => i.VariantId, i => i.Quantity);
        // 固定顺序锁定涉及的库存行，保证仓库选择与预占之间不会被其他订单抢走。
        var stocks = await db
            .Stocks.FromSql(
                $"SELECT * FROM inventory.stocks WHERE variant_id = ANY({ids}) ORDER BY warehouse_id,variant_id FOR UPDATE"
            )
            .ToListAsync(ct);
        var warehouse = stocks
            .GroupBy(s => s.WarehouseId)
            .FirstOrDefault(g =>
                needed.All(n =>
                    g.Any(s =>
                        s.VariantId == n.Key && s.QuantityOnHand - s.QuantityReserved >= n.Value
                    )
                )
            );
        if (warehouse is null)
            throw ApiError.Conflict("没有可完整履约的仓库，请减少商品数量或拆单。");
        var now = DateTime.UtcNow;
        var totals = data.Quote.Totals;
        var order = new Order
        {
            CustomerId = customer,
            OrderNumber = $"MS-{now:yyyyMMdd}-{Guid.NewGuid():N}",
            Status = "confirmed",
            Currency = "JPY",
            Subtotal = totals.Subtotal,
            DiscountTotal = totals.DiscountTotal,
            TaxTotal = totals.TaxTotal,
            ShippingTotal = totals.ShippingTotal,
            GrandTotal = totals.GrandTotal,
            PlacedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        // 数据库订单号上限 40 字符；日期 + 随机片段兼顾可读性与唯一性。
        order.OrderNumber = $"MS-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..35];
        foreach (var line in data.Quote.Lines)
        {
            var item = data.Cart.CartItems.Single(i => i.Variant.PublicId == line.VariantId);
            order.OrderItems.Add(
                new OrderItem
                {
                    ProductId = item.Variant.ProductId,
                    VariantId = item.VariantId,
                    Sku = line.Sku,
                    ProductName = line.ProductName,
                    VariantName = line.VariantName,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountAmount = line.DiscountAmount,
                    TaxAmount = line.TaxAmount,
                    LineTotal = line.LineTotal,
                    CreatedAt = now,
                }
            );
        }
        foreach (var type in new[] { "shipping", "billing" })
        {
            var a = data.Address;
            order.OrderAddresses.Add(
                new()
                {
                    AddressType = type,
                    RecipientName = a.RecipientName,
                    PostalCode = a.PostalCode,
                    CountryCode = a.CountryCode,
                    Prefecture = a.Prefecture,
                    City = a.City,
                    AddressLine1 = a.AddressLine1,
                    AddressLine2 = a.AddressLine2,
                    Phone = a.Phone,
                }
            );
        }
        order.OrderStatusHistories.Add(
            new()
            {
                FromStatus = null,
                ToStatus = "pending",
                Reason = "客户提交订单",
                CreatedAt = now,
            }
        );
        order.OrderStatusHistories.Add(
            new()
            {
                FromStatus = "pending",
                ToStatus = "confirmed",
                Reason = "服务端报价与库存校验通过",
                CreatedAt = now.AddTicks(10),
            }
        );
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        foreach (var item in order.OrderItems.OrderBy(i => i.VariantId))
            await db.Database.ExecuteSqlAsync(
                $"SELECT inventory.reserve_stock({warehouse.Key},{item.VariantId},{item.Quantity},{order.Id})",
                ct
            );
        if (data.Coupon != null && totals.DiscountTotal > 0)
        {
            // 通过锁定优惠券行串行化 used_count，防止同时突破使用上限。
            var coupon = await db.Coupons.SingleAsync(c => c.Id == data.Coupon.Id, ct);
            coupon.UsedCount++;
            db.CouponRedemptions.Add(
                new()
                {
                    CouponId = coupon.Id,
                    CustomerId = customer,
                    OrderId = order.Id,
                    DiscountAmount = totals.DiscountTotal,
                    RedeemedAt = now,
                }
            );
        }
        var cart = await db.Carts.SingleAsync(c => c.Id == data.Cart.Id, ct);
        cart.Status = "converted";
        cart.CheckedOutAt = now;
        cart.UpdatedAt = now;
        db.CheckoutRequests.Add(
            new()
            {
                CustomerId = customer,
                RequestKey = key,
                RequestHash = requestHash,
                OrderId = order.Id,
                CreatedAt = now,
            }
        );
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(order.PublicId, order.OrderNumber, order.Status, order.GrandTotal, false);
    }

    private async Task<CheckoutData> ReadAsync(
        long customer,
        QuoteRequest request,
        CancellationToken ct
    )
    {
        var address =
            await db
                .CustomerAddresses.AsNoTracking()
                .SingleOrDefaultAsync(
                    a =>
                        a.PublicId == request.AddressId
                        && a.CustomerId == customer
                        && a.AddressType == "shipping",
                    ct
                )
            ?? throw new ApiError(400, "address", "请选择自己的有效收货地址。");
        var cart =
            await db
                .Carts.AsNoTracking()
                .AsSplitQuery()
                .Include(c => c.CartItems)
                    .ThenInclude(i => i.Variant)
                        .ThenInclude(v => v.Product)
                            .ThenInclude(p => p.Categories)
                .SingleOrDefaultAsync(c => c.CustomerId == customer && c.Status == "active", ct)
            ?? throw new ApiError(400, "cart_empty", "购物车为空。");
        var lines = cart
            .CartItems.OrderBy(i => i.VariantId)
            .Select(i =>
            {
                if (
                    !i.Variant.IsActive
                    || i.Variant.Product.Status != "active"
                    || i.Variant.Product.DeletedAt != null
                    || i.Variant.Currency != "JPY"
                )
                    throw ApiError.Conflict("商品已下架或币种不受支持。");
                return new PriceLine(
                    i.Variant.PublicId,
                    i.Variant.Sku,
                    i.Variant.Product.Name,
                    i.Variant.Name,
                    i.Quantity,
                    i.Variant.Price,
                    CatalogEndpoints.Food(i.Variant.Product) ? .08m : .10m
                );
            })
            .ToList();
        var subtotal = lines.Sum(l => l.Quantity * l.UnitPrice);
        Coupon? coupon = null;
        var discount = 0m;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var code = Rules.Text(request.CouponCode, 40, "优惠码").ToUpperInvariant();
            coupon = await db.Coupons.AsNoTracking().SingleOrDefaultAsync(c => c.Code == code, ct);
            var now = DateTime.UtcNow;
            if (
                coupon is null
                || !coupon.IsActive
                || now < coupon.StartsAt
                || now > coupon.EndsAt
                || coupon.UsageLimit.HasValue && coupon.UsedCount >= coupon.UsageLimit
                || subtotal < coupon.MinOrderAmount
            )
                throw new ApiError(
                    422,
                    "coupon_unavailable",
                    "优惠券无效、已过期、额度用尽或未达到门槛。"
                );
            discount = Math.Min(
                subtotal,
                Math.Min(
                    coupon.MaxDiscountAmount ?? subtotal,
                    coupon.DiscountType == "fixed"
                        ? coupon.DiscountValue
                        : decimal.Floor(subtotal * coupon.DiscountValue / 100)
                )
            );
        }
        return new(
            cart,
            address,
            coupon,
            Pricing.Calculate(lines, discount, request.ShippingMethod)
        );
    }
}
