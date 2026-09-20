using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Features.Marketing;
using MiniStore.Features.Orders;
using MiniStore.Features.Products;

namespace MiniStore.Features.Checkout;

/// <summary>一个作用域一个 DbContext。构造参数由 ASP.NET Core 的依赖注入容器提供，不能把此服务注册成单例。</summary>
public sealed class CheckoutService(StoreDbContext db, IDataProtectionProvider protection)
{
    private readonly ITimeLimitedDataProtector protector = protection
        .CreateProtector("MiniStore.Checkout.v1")
        .ToTimeLimitedDataProtector();

    /// <summary>生成内容摘要用于相等性比较；普通哈希本身不防篡改，报价令牌另由 Data Protection 保护。</summary>
    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>将当前计价结果、配送方式、优惠码和地址快照纳入摘要，用于检测报价后内容变化。</summary>
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

    /// <summary>重新读取当前购物车和地址，计算报价并生成十分钟有效的受保护令牌；此时不锁库存。</summary>
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

    /// <summary>在一个事务中验证幂等请求和报价，创建订单快照、预占库存、记录优惠核销并转换购物车。</summary>
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
        // await using 会在离开作用域时释放事务；只有走到 CommitAsync 才提交，异常路径不会留下半张订单。
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
        // FromSql 接收插值字符串并参数化，数组作为参数绑定；不要先拼接成普通 SQL 字符串。
        // 固定 ID 顺序取锁，降低多个事务以相反顺序等待造成死锁的机会。
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
            // 复制名称与成交金额而不是只存外键，商品以后改名、调价也不能改写历史订单。
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
            // 当前学习版把同一收货地址复制为收货、账单两份快照；暂不支持分别选择账单地址。
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
        // 先 SaveChanges 取得数据库生成的内部订单 ID，供预占函数使用；外层事务尚未提交。
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

    /// <summary>从数据库读取本人的收货地址、当前规格价格与优惠券规则，返回服务端计算的结算数据。</summary>
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
                    ProductService.IsFood(i.Variant.Product) ? .08m : .10m
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
