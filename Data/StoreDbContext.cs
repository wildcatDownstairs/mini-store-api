using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MiniStore.Data.Entities;

namespace MiniStore.Data;

public partial class StoreDbContext : DbContext
{
    public StoreDbContext(DbContextOptions<StoreDbContext> options)
        : base(options) { }

    public virtual DbSet<AdminUser> AdminUsers { get; set; }

    public virtual DbSet<Brand> Brands { get; set; }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<CheckoutRequest> CheckoutRequests { get; set; }

    public virtual DbSet<Coupon> Coupons { get; set; }

    public virtual DbSet<CouponRedemption> CouponRedemptions { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<CustomerAddress> CustomerAddresses { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderAddress> OrderAddresses { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }

    public virtual DbSet<OrderSummary> OrderSummaries { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<ProductInventorySummary> ProductInventorySummaries { get; set; }

    public virtual DbSet<ProductReview> ProductReviews { get; set; }

    public virtual DbSet<ProductSalesSummary> ProductSalesSummaries { get; set; }

    public virtual DbSet<ProductVariant> ProductVariants { get; set; }

    public virtual DbSet<Refund> Refunds { get; set; }

    public virtual DbSet<Shipment> Shipments { get; set; }

    public virtual DbSet<Stock> Stocks { get; set; }

    public virtual DbSet<StockMovement> StockMovements { get; set; }

    public virtual DbSet<Warehouse> Warehouses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("admin_users_pkey");

            entity.ToTable(
                "admin_users",
                "account",
                tb =>
                    tb.HasComment(
                        "后台人员账户：与商城客户身份分离；运营人员可写，只读人员只能查询。"
                    )
            );

            entity.HasIndex(e => e.PublicId, "admin_users_public_id_key").IsUnique();

            entity
                .Property(e => e.Id)
                .HasComment("管理员内部自增主键。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("管理员账户创建时间。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.DisplayName)
                .HasMaxLength(80)
                .HasComment("后台人员显示名称。")
                .HasColumnName("display_name");
            entity
                .Property(e => e.Email)
                .HasMaxLength(254)
                .HasComment("登录邮箱，不区分大小写唯一。")
                .HasColumnName("email");
            entity
                .Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasComment("是否允许登录及访问后台。")
                .HasColumnName("is_active");
            entity
                .Property(e => e.PasswordHash)
                .HasComment("ASP.NET Core PasswordHasher 生成的加盐密码哈希，不保存明文。")
                .HasColumnName("password_hash");
            entity
                .Property(e => e.PublicId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasComment("管理员对外 UUID 标识。")
                .HasColumnName("public_id");
            entity
                .Property(e => e.Role)
                .HasMaxLength(16)
                .HasComment("角色：operator 运营人员；viewer 只读人员。")
                .HasColumnName("role");
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("brands_pkey");

            entity.ToTable(
                "brands",
                "catalog",
                tb => tb.HasComment("商品品牌：保存品牌名称、访问标识和所属国家。")
            );

            entity.HasIndex(e => e.PublicId, "brands_public_id_key").IsUnique();

            entity.HasIndex(e => e.Slug, "brands_slug_key").IsUnique();

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.CountryCode)
                .HasMaxLength(2)
                .IsFixedLength()
                .HasComment("品牌所属国家或地区的两位代码，例如 JP。")
                .HasColumnName("country_code");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.Name)
                .HasMaxLength(120)
                .HasComment("品牌显示名称。")
                .HasColumnName("name");
            entity
                .Property(e => e.PublicId)
                .HasDefaultValueSql("uuidv7()")
                .HasComment(
                    "对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。"
                )
                .HasColumnName("public_id");
            entity
                .Property(e => e.Slug)
                .HasMaxLength(160)
                .HasComment("用于页面地址或 API 查询的可读唯一标识。")
                .HasColumnName("slug");
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("carts_pkey");

            entity.ToTable(
                "carts",
                "sales",
                tb => tb.HasComment("客户购物车：每位客户最多一个使用中的购物车。")
            );

            entity
                .HasIndex(e => e.CustomerId, "carts_active_customer_uq")
                .IsUnique()
                .HasFilter("((status)::text = 'active'::text)");

            entity.HasIndex(e => e.CustomerId, "carts_customer_idx");

            entity.HasIndex(e => e.PublicId, "carts_public_id_key").IsUnique();

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.CheckedOutAt)
                .HasComment("购物车转为订单的时间；仅 converted 状态非空。")
                .HasColumnName("checked_out_at");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.CustomerId)
                .HasComment("所属客户的内部主键，关联 account.customers.id。")
                .HasColumnName("customer_id");
            entity
                .Property(e => e.PublicId)
                .HasDefaultValueSql("uuidv7()")
                .HasComment(
                    "对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。"
                )
                .HasColumnName("public_id");
            entity
                .Property(e => e.Status)
                .HasMaxLength(16)
                .HasComment("购物车状态：active 使用中、converted 已转订单、abandoned 已放弃。")
                .HasColumnName("status");
            entity
                .Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录最后更新时间，由写入方显式维护。")
                .HasColumnName("updated_at");

            entity
                .HasOne(d => d.Customer)
                .WithMany(p => p.Carts)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("carts_customer_id_fkey");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("cart_items_pkey");

            entity.ToTable(
                "cart_items",
                "sales",
                tb =>
                    tb.HasComment(
                        "购物车商品项：记录变体、数量及当时单价；结算时应重新校验价格与库存。"
                    )
            );

            entity
                .HasIndex(e => new { e.CartId, e.VariantId }, "cart_items_cart_id_variant_id_key")
                .IsUnique();

            entity.HasIndex(e => e.VariantId, "cart_items_variant_idx");

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.CartId)
                .HasComment("购物车内部主键，关联 sales.carts.id。")
                .HasColumnName("cart_id");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.Quantity)
                .HasComment("购买数量，必须为正整数。")
                .HasColumnName("quantity");
            entity
                .Property(e => e.UnitPrice)
                .HasPrecision(12, 2)
                .HasComment(
                    "加入购物车时记录的变体税前单价；结算时需重新确认，不代表已锁定成交价格。"
                )
                .HasColumnName("unit_price");
            entity
                .Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录最后更新时间，由写入方显式维护。")
                .HasColumnName("updated_at");
            entity
                .Property(e => e.VariantId)
                .HasComment("商品变体内部主键，关联 catalog.product_variants.id。")
                .HasColumnName("variant_id");

            entity
                .HasOne(d => d.Cart)
                .WithMany(p => p.CartItems)
                .HasForeignKey(d => d.CartId)
                .HasConstraintName("cart_items_cart_id_fkey");

            entity
                .HasOne(d => d.Variant)
                .WithMany(p => p.CartItems)
                .HasForeignKey(d => d.VariantId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("cart_items_variant_id_fkey");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("categories_pkey");

            entity.ToTable(
                "categories",
                "catalog",
                tb => tb.HasComment("树形商品分类：通过父分类关联支持多层目录。")
            );

            entity.HasIndex(e => e.ParentId, "categories_parent_idx");

            entity.HasIndex(e => e.PublicId, "categories_public_id_key").IsUnique();

            entity.HasIndex(e => e.Slug, "categories_slug_key").IsUnique();

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasComment("分类是否启用，控制目录展示。")
                .HasColumnName("is_active");
            entity
                .Property(e => e.Name)
                .HasMaxLength(120)
                .HasComment("分类显示名称。")
                .HasColumnName("name");
            entity
                .Property(e => e.ParentId)
                .HasComment(
                    "父分类内部主键，关联 catalog.categories.id；为空表示根分类，不允许形成循环。"
                )
                .HasColumnName("parent_id");
            entity
                .Property(e => e.PublicId)
                .HasDefaultValueSql("uuidv7()")
                .HasComment(
                    "对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。"
                )
                .HasColumnName("public_id");
            entity
                .Property(e => e.Slug)
                .HasMaxLength(160)
                .HasComment("用于页面地址或 API 查询的可读唯一标识。")
                .HasColumnName("slug");
            entity
                .Property(e => e.SortOrder)
                .HasComment("显示排序值，通常按升序展示。")
                .HasColumnName("sort_order");

            entity
                .HasOne(d => d.Parent)
                .WithMany(p => p.InverseParent)
                .HasForeignKey(d => d.ParentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("categories_parent_id_fkey");
        });

        modelBuilder.Entity<CheckoutRequest>(entity =>
        {
            entity
                .HasKey(e => new { e.CustomerId, e.RequestKey })
                .HasName("checkout_requests_pkey");

            entity.ToTable(
                "checkout_requests",
                "sales",
                tb =>
                    tb.HasComment(
                        "已完成下单请求的幂等记录：相同客户和键返回原订单；同键不同请求拒绝。与订单在同一事务提交。"
                    )
            );

            entity
                .Property(e => e.CustomerId)
                .HasComment("发起下单的客户内部主键。")
                .HasColumnName("customer_id");
            entity
                .Property(e => e.RequestKey)
                .HasMaxLength(128)
                .HasComment("客户端 Idempotency-Key，长度最多 128，客户端重试需复用。")
                .HasColumnName("request_key");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("幂等请求成功提交时间。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.OrderId)
                .HasComment("成功创建的订单内部主键。")
                .HasColumnName("order_id");
            entity
                .Property(e => e.RequestHash)
                .HasMaxLength(64)
                .HasComment("请求内容的 SHA-256 摘要，避免同一幂等键被用于不同请求。")
                .HasColumnName("request_hash");

            entity
                .HasOne(d => d.Customer)
                .WithMany(p => p.CheckoutRequests)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("checkout_requests_customer_id_fkey");

            entity
                .HasOne(d => d.Order)
                .WithMany(p => p.CheckoutRequests)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("checkout_requests_order_id_fkey");
        });

        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("coupons_pkey");

            entity.ToTable(
                "coupons",
                "marketing",
                tb => tb.HasComment("优惠券规则：定义折扣、门槛、有效期和使用次数限制。")
            );

            entity.HasIndex(e => e.Code, "coupons_code_key").IsUnique();

            entity.HasIndex(e => e.PublicId, "coupons_public_id_key").IsUnique();

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.Code)
                .HasMaxLength(40)
                .HasComment("唯一优惠码，供客户结算时输入；当前唯一约束区分大小写。")
                .HasColumnName("code");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.DiscountType)
                .HasMaxLength(16)
                .HasComment("优惠类型：percentage 按百分比减免，fixed 固定金额减免。")
                .HasColumnName("discount_type");
            entity
                .Property(e => e.DiscountValue)
                .HasPrecision(12, 2)
                .HasComment(
                    "优惠数值；percentage 时为减免百分比（10 表示减免 10%，即九折），fixed 时为固定减免金额。"
                )
                .HasColumnName("discount_value");
            entity
                .Property(e => e.EndsAt)
                .HasComment("优惠券有效期结束时间，必须晚于开始时间。")
                .HasColumnName("ends_at");
            entity
                .Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasComment("优惠券是否启用；核销仍需校验有效期、金额门槛及使用次数。")
                .HasColumnName("is_active");
            entity
                .Property(e => e.MaxDiscountAmount)
                .HasPrecision(12, 2)
                .HasComment("单次优惠金额上限；为空表示不单独限制，但优惠不得超过商品小计。")
                .HasColumnName("max_discount_amount");
            entity
                .Property(e => e.MinOrderAmount)
                .HasPrecision(12, 2)
                .HasComment("使用优惠券所需的折扣前税前商品最低金额，不含运费。")
                .HasColumnName("min_order_amount");
            entity
                .Property(e => e.Name)
                .HasMaxLength(120)
                .HasComment("优惠券活动显示名称。")
                .HasColumnName("name");
            entity
                .Property(e => e.PublicId)
                .HasDefaultValueSql("uuidv7()")
                .HasComment(
                    "对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。"
                )
                .HasColumnName("public_id");
            entity
                .Property(e => e.StartsAt)
                .HasComment("优惠券有效期开始时间。")
                .HasColumnName("starts_at");
            entity
                .Property(e => e.UsageLimit)
                .HasComment("优惠券最多可核销次数；为空表示不限次数。")
                .HasColumnName("usage_limit");
            entity
                .Property(e => e.UsedCount)
                .HasComment("已核销次数，应与核销记录数一致，由业务事务维护。")
                .HasColumnName("used_count");
        });

        modelBuilder.Entity<CouponRedemption>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("coupon_redemptions_pkey");

            entity.ToTable(
                "coupon_redemptions",
                "marketing",
                tb =>
                    tb.HasComment(
                        "优惠券核销记录：关联客户和订单，保存实际优惠金额；当前每笔订单最多使用一张优惠券。"
                    )
            );

            entity.HasIndex(e => e.OrderId, "coupon_redemptions_order_id_key").IsUnique();

            entity.HasIndex(e => e.CouponId, "redemptions_coupon_idx");

            entity.HasIndex(e => e.CustomerId, "redemptions_customer_idx");

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.CouponId)
                .HasComment("优惠券内部主键，关联 marketing.coupons.id。")
                .HasColumnName("coupon_id");
            entity
                .Property(e => e.CustomerId)
                .HasComment("所属客户的内部主键，关联 account.customers.id。")
                .HasColumnName("customer_id");
            entity
                .Property(e => e.DiscountAmount)
                .HasPrecision(12, 2)
                .HasComment(
                    "本次订单实际使用优惠券减免的税前商品金额，应等于该订单 discount_total。"
                )
                .HasColumnName("discount_amount");
            entity
                .Property(e => e.OrderId)
                .HasComment("订单内部主键，关联 sales.orders.id。")
                .HasColumnName("order_id");
            entity
                .Property(e => e.RedeemedAt)
                .HasDefaultValueSql("now()")
                .HasComment("优惠券实际核销时间。")
                .HasColumnName("redeemed_at");

            entity
                .HasOne(d => d.Coupon)
                .WithMany(p => p.CouponRedemptions)
                .HasForeignKey(d => d.CouponId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("coupon_redemptions_coupon_id_fkey");

            entity
                .HasOne(d => d.Customer)
                .WithMany(p => p.CouponRedemptions)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("coupon_redemptions_customer_id_fkey");

            entity
                .HasOne(d => d.Order)
                .WithOne(p => p.CouponRedemption)
                .HasForeignKey<CouponRedemption>(d => d.OrderId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("coupon_redemptions_order_id_fkey");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("customers_pkey");

            entity.ToTable(
                "customers",
                "account",
                tb =>
                    tb.HasComment(
                        "客户账户：保存客户身份、联系信息和账户状态；删除优先使用软删除。"
                    )
            );

            entity.HasIndex(e => e.PublicId, "customers_public_id_key").IsUnique();

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.BirthDate)
                .HasComment("出生日期，仅保存日期，不含时分秒。")
                .HasColumnName("birth_date");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.DeletedAt)
                .HasComment("软删除时间；为空表示未软删除。")
                .HasColumnName("deleted_at");
            entity
                .Property(e => e.Email)
                .HasMaxLength(254)
                .HasComment(
                    "客户邮箱；通过 lower(email) 唯一索引保证大小写不敏感的唯一性，软删除后仍保留唯一约束。"
                )
                .HasColumnName("email");
            entity
                .Property(e => e.EmailVerifiedAt)
                .HasComment("邮箱验证通过时间；为空表示尚未验证。")
                .HasColumnName("email_verified_at");
            entity
                .Property(e => e.FirstName)
                .HasMaxLength(80)
                .HasComment("客户名字，不含姓氏。")
                .HasColumnName("first_name");
            entity
                .Property(e => e.LastLoginAt)
                .HasComment("最近一次登录时间；为空表示没有登录记录。")
                .HasColumnName("last_login_at");
            entity
                .Property(e => e.LastName)
                .HasMaxLength(80)
                .HasComment("客户姓氏。")
                .HasColumnName("last_name");
            entity
                .Property(e => e.PasswordHash)
                .HasComment(
                    "密码哈希；实验数据为不可用于登录的假 Argon2 风格字符串，禁止保存明文密码。"
                )
                .HasColumnName("password_hash");
            entity
                .Property(e => e.Phone)
                .HasMaxLength(24)
                .HasComment("联系电话，包含必要的国家或地区拨号信息；实验数据为合成号码。")
                .HasColumnName("phone");
            entity
                .Property(e => e.PublicId)
                .HasDefaultValueSql("uuidv7()")
                .HasComment(
                    "对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。"
                )
                .HasColumnName("public_id");
            entity
                .Property(e => e.Status)
                .HasMaxLength(16)
                .HasComment("账户状态：active 正常、disabled 停用、pending 待激活。")
                .HasColumnName("status");
            entity
                .Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录最后更新时间，由写入方显式维护。")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<CustomerAddress>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("customer_addresses_pkey");

            entity.ToTable(
                "customer_addresses",
                "account",
                tb =>
                    tb.HasComment(
                        "客户当前地址簿：支持收货地址、账单地址及各类型默认地址；修改不会影响历史订单地址快照。"
                    )
            );

            entity
                .HasIndex(e => new { e.CustomerId, e.AddressType }, "address_default_uq")
                .IsUnique()
                .HasFilter("is_default");

            entity.HasIndex(e => e.PublicId, "addresses_public_id_uq").IsUnique();

            entity.HasIndex(e => e.CustomerId, "customer_addresses_customer_idx");

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.AddressLine1)
                .HasMaxLength(160)
                .HasComment("详细地址第一行，通常包含町名、丁目及门牌号。")
                .HasColumnName("address_line1");
            entity
                .Property(e => e.AddressLine2)
                .HasMaxLength(160)
                .HasComment("详细地址第二行，通常包含楼名和房间号，可为空。")
                .HasColumnName("address_line2");
            entity
                .Property(e => e.AddressType)
                .HasMaxLength(16)
                .HasComment("地址类型：shipping 表示收货地址，billing 表示账单地址。")
                .HasColumnName("address_type");
            entity
                .Property(e => e.City)
                .HasMaxLength(80)
                .HasComment("市、区、町或村名称。")
                .HasColumnName("city");
            entity
                .Property(e => e.CountryCode)
                .HasMaxLength(2)
                .HasDefaultValueSql("'JP'::bpchar")
                .IsFixedLength()
                .HasComment("两位国家或地区代码，例如 JP 表示日本。")
                .HasColumnName("country_code");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.CustomerId)
                .HasComment("所属客户的内部主键，关联 account.customers.id。")
                .HasColumnName("customer_id");
            entity
                .Property(e => e.IsDefault)
                .HasComment(
                    "是否为该客户在此地址类型下的默认地址；同一客户和地址类型最多一条默认地址。"
                )
                .HasColumnName("is_default");
            entity
                .Property(e => e.Phone)
                .HasMaxLength(24)
                .HasComment("联系电话，包含必要的国家或地区拨号信息；实验数据为合成号码。")
                .HasColumnName("phone");
            entity
                .Property(e => e.PostalCode)
                .HasMaxLength(12)
                .HasComment("邮政编码；日本地址通常采用三位数字加连字符加四位数字。")
                .HasColumnName("postal_code");
            entity
                .Property(e => e.Prefecture)
                .HasMaxLength(80)
                .HasComment("都道府县名称，例如东京都或大阪府。")
                .HasColumnName("prefecture");
            entity
                .Property(e => e.PublicId)
                .HasDefaultValueSql("uuidv7()")
                .HasComment("地址对外 UUID 标识；用于 API 定位，仍须校验客户归属。")
                .HasColumnName("public_id");
            entity
                .Property(e => e.RecipientName)
                .HasMaxLength(160)
                .HasComment("收件人或账单接收人姓名。")
                .HasColumnName("recipient_name");
            entity
                .Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录最后更新时间，由写入方显式维护。")
                .HasColumnName("updated_at");

            entity
                .HasOne(d => d.Customer)
                .WithMany(p => p.CustomerAddresses)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("customer_addresses_customer_id_fkey");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("orders_pkey");

            entity.ToTable(
                "orders",
                "sales",
                tb =>
                    tb.HasComment(
                        "订单主表：保存成交金额、订单状态与关键时间；与明细、支付和物流共同组成订单业务。"
                    )
            );

            entity.HasIndex(e => new { e.CreatedAt, e.Id }, "orders_created_idx").IsDescending();

            entity
                .HasIndex(
                    e => new
                    {
                        e.CustomerId,
                        e.CreatedAt,
                        e.Id,
                    },
                    "orders_customer_created_idx"
                )
                .IsDescending(false, true, true);

            entity.HasIndex(e => e.OrderNumber, "orders_order_number_key").IsUnique();

            entity.HasIndex(e => e.PublicId, "orders_public_id_key").IsUnique();

            entity
                .HasIndex(
                    e => new
                    {
                        e.Status,
                        e.CreatedAt,
                        e.Id,
                    },
                    "orders_status_created_idx"
                )
                .IsDescending(false, true, true);

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.CancelledAt)
                .HasComment("订单取消时间；仅 cancelled 状态非空。")
                .HasColumnName("cancelled_at");
            entity
                .Property(e => e.CompletedAt)
                .HasComment(
                    "订单送达完成时间；delivered 和 returned 状态必须有值，退货后保留原完成时间。"
                )
                .HasColumnName("completed_at");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.Currency)
                .HasMaxLength(3)
                .HasDefaultValueSql("'JPY'::bpchar")
                .IsFixedLength()
                .HasComment(
                    "三位货币代码，例如 JPY（日元）或 USD（美元）；金额必须按币种分别计算。"
                )
                .HasColumnName("currency");
            entity
                .Property(e => e.CustomerId)
                .HasComment("所属客户的内部主键，关联 account.customers.id。")
                .HasColumnName("customer_id");
            entity
                .Property(e => e.DiscountTotal)
                .HasPrecision(12, 2)
                .HasComment("订单优惠总额，等于订单项分摊折扣之和，与优惠券实际核销金额对应。")
                .HasColumnName("discount_total");
            entity
                .Property(e => e.GrandTotal)
                .HasPrecision(12, 2)
                .HasComment("订单应付总额，等于商品小计减优惠总额加税额加配送费。")
                .HasColumnName("grand_total");
            entity
                .Property(e => e.OrderNumber)
                .HasMaxLength(40)
                .HasComment("唯一的可读订单编号，供客户查询和业务对账使用。")
                .HasColumnName("order_number");
            entity
                .Property(e => e.PaidAt)
                .HasComment("订单付款成功时间；为空表示尚未记录付款成功。")
                .HasColumnName("paid_at");
            entity
                .Property(e => e.PlacedAt)
                .HasComment("客户下单时间。")
                .HasColumnName("placed_at");
            entity
                .Property(e => e.PublicId)
                .HasDefaultValueSql("uuidv7()")
                .HasComment(
                    "对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。"
                )
                .HasColumnName("public_id");
            entity
                .Property(e => e.ShippingTotal)
                .HasPrecision(12, 2)
                .HasComment("最终收取的配送费，不参与商品折扣计算。")
                .HasColumnName("shipping_total");
            entity
                .Property(e => e.Status)
                .HasMaxLength(16)
                .HasComment(
                    "订单状态：pending 待确认、confirmed 已确认、paid 已付款、processing 处理中、shipped 已发货、delivered 已送达、cancelled 已取消、returned 已退货。"
                )
                .HasColumnName("status");
            entity
                .Property(e => e.Subtotal)
                .HasPrecision(12, 2)
                .HasComment("折扣前税前商品金额，等于所有订单项数量乘单价之和。")
                .HasColumnName("subtotal");
            entity
                .Property(e => e.TaxTotal)
                .HasPrecision(12, 2)
                .HasComment("折后商品消费税总额，等于订单项税额之和。")
                .HasColumnName("tax_total");
            entity
                .Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录最后更新时间，由写入方显式维护。")
                .HasColumnName("updated_at");

            entity
                .HasOne(d => d.Customer)
                .WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("orders_customer_id_fkey");
        });

        modelBuilder.Entity<OrderAddress>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("order_addresses_pkey");

            entity.ToTable(
                "order_addresses",
                "sales",
                tb =>
                    tb.HasComment("订单地址快照：保存下单时收货及账单地址，不依赖客户当前地址簿。")
            );

            entity
                .HasIndex(
                    e => new { e.OrderId, e.AddressType },
                    "order_addresses_order_id_address_type_key"
                )
                .IsUnique();

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.AddressLine1)
                .HasMaxLength(160)
                .HasComment("下单时的地址快照：详细地址第一行，通常包含町名、丁目及门牌号。")
                .HasColumnName("address_line1");
            entity
                .Property(e => e.AddressLine2)
                .HasMaxLength(160)
                .HasComment("下单时的地址快照：详细地址第二行，通常包含楼名和房间号，可为空。")
                .HasColumnName("address_line2");
            entity
                .Property(e => e.AddressType)
                .HasMaxLength(16)
                .HasComment(
                    "下单时的地址快照：地址类型：shipping 表示收货地址，billing 表示账单地址。"
                )
                .HasColumnName("address_type");
            entity
                .Property(e => e.City)
                .HasMaxLength(80)
                .HasComment("下单时的地址快照：市、区、町或村名称。")
                .HasColumnName("city");
            entity
                .Property(e => e.CountryCode)
                .HasMaxLength(2)
                .IsFixedLength()
                .HasComment("下单时的地址快照：两位国家或地区代码，例如 JP 表示日本。")
                .HasColumnName("country_code");
            entity
                .Property(e => e.OrderId)
                .HasComment("订单内部主键，关联 sales.orders.id。")
                .HasColumnName("order_id");
            entity
                .Property(e => e.Phone)
                .HasMaxLength(24)
                .HasComment(
                    "下单时的地址快照：联系电话，包含必要的国家或地区拨号信息；实验数据为合成号码。"
                )
                .HasColumnName("phone");
            entity
                .Property(e => e.PostalCode)
                .HasMaxLength(12)
                .HasComment(
                    "下单时的地址快照：邮政编码；日本地址通常采用三位数字加连字符加四位数字。"
                )
                .HasColumnName("postal_code");
            entity
                .Property(e => e.Prefecture)
                .HasMaxLength(80)
                .HasComment("下单时的地址快照：都道府县名称，例如东京都或大阪府。")
                .HasColumnName("prefecture");
            entity
                .Property(e => e.RecipientName)
                .HasMaxLength(160)
                .HasComment("下单时的地址快照：收件人或账单接收人姓名。")
                .HasColumnName("recipient_name");

            entity
                .HasOne(d => d.Order)
                .WithMany(p => p.OrderAddresses)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("order_addresses_order_id_fkey");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("order_items_pkey");

            entity.ToTable(
                "order_items",
                "sales",
                tb =>
                    tb.HasComment(
                        "订单商品快照：保留下单时的 SKU、名称、成交价格及折扣税额，后续商品修改不影响历史订单。"
                    )
            );

            entity.HasIndex(e => e.OrderId, "order_items_order_idx");

            entity.HasIndex(e => e.ProductId, "order_items_product_idx");

            entity.HasIndex(e => e.PublicId, "order_items_public_id_uq").IsUnique();

            entity.HasIndex(e => e.VariantId, "order_items_variant_idx");

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.DiscountAmount)
                .HasPrecision(12, 2)
                .HasComment("分摊至此订单项的整行折扣金额，不是单件折扣，不能超过数量乘单价。")
                .HasColumnName("discount_amount");
            entity
                .Property(e => e.LineTotal)
                .HasPrecision(12, 2)
                .HasComment("订单项含税净额，等于数量乘税前单价减整行折扣加整行税额，不含运费。")
                .HasColumnName("line_total");
            entity
                .Property(e => e.OrderId)
                .HasComment("订单内部主键，关联 sales.orders.id。")
                .HasColumnName("order_id");
            entity
                .Property(e => e.ProductId)
                .HasComment("商品内部主键，关联 catalog.products.id。")
                .HasColumnName("product_id");
            entity
                .Property(e => e.ProductName)
                .HasMaxLength(200)
                .HasComment("下单时的商品名称快照，不随商品改名而更新。")
                .HasColumnName("product_name");
            entity
                .Property(e => e.PublicId)
                .HasDefaultValueSql("uuidv7()")
                .HasComment("订单项对外 UUID 标识；用于提交购买评价，仍须校验订单归属。")
                .HasColumnName("public_id");
            entity
                .Property(e => e.Quantity)
                .HasComment("购买数量，必须为正整数。")
                .HasColumnName("quantity");
            entity
                .Property(e => e.Sku)
                .HasMaxLength(64)
                .HasComment("下单时的商品变体 SKU 快照，不随当前 SKU 修改而更新。")
                .HasColumnName("sku");
            entity
                .Property(e => e.TaxAmount)
                .HasPrecision(12, 2)
                .HasComment("此订单项在折扣分摊后的消费税额；为整行税额。")
                .HasColumnName("tax_amount");
            entity
                .Property(e => e.UnitPrice)
                .HasPrecision(12, 2)
                .HasComment("下单成交时的税前单价快照，后续调价不影响该值。")
                .HasColumnName("unit_price");
            entity
                .Property(e => e.VariantId)
                .HasComment("商品变体内部主键，关联 catalog.product_variants.id。")
                .HasColumnName("variant_id");
            entity
                .Property(e => e.VariantName)
                .HasMaxLength(120)
                .HasComment("下单时的变体名称快照，不随规格改名而更新。")
                .HasColumnName("variant_name");

            entity
                .HasOne(d => d.Order)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("order_items_order_id_fkey");

            entity
                .HasOne(d => d.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("order_items_product_id_fkey");

            entity
                .HasOne(d => d.ProductVariant)
                .WithMany(p => p.OrderItems)
                .HasPrincipalKey(p => new { p.Id, p.ProductId })
                .HasForeignKey(d => new { d.VariantId, d.ProductId })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("order_items_variant_id_product_id_fkey");
        });

        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("order_status_history_pkey");

            entity.ToTable(
                "order_status_history",
                "sales",
                tb =>
                    tb.HasComment("订单状态流转记录：按时间保存前后状态及变更原因，供追踪和审计。")
            );

            entity.HasIndex(
                e => new
                {
                    e.OrderId,
                    e.CreatedAt,
                    e.Id,
                },
                "order_history_order_time_idx"
            );

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("本次订单状态变更发生时间。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.FromStatus)
                .HasMaxLength(16)
                .HasComment("变更前订单状态；首条记录为空，表示订单刚创建。")
                .HasColumnName("from_status");
            entity
                .Property(e => e.OrderId)
                .HasComment("订单内部主键，关联 sales.orders.id。")
                .HasColumnName("order_id");
            entity
                .Property(e => e.Reason)
                .HasComment("订单状态变更原因，可为空。")
                .HasColumnName("reason");
            entity
                .Property(e => e.ToStatus)
                .HasMaxLength(16)
                .HasComment(
                    "变更后订单状态：pending 待确认、confirmed 已确认、paid 已付款、processing 处理中、shipped 已发货、delivered 已送达、cancelled 已取消、returned 已退货；仅允许约束定义的状态流转。"
                )
                .HasColumnName("to_status");

            entity
                .HasOne(d => d.Order)
                .WithMany(p => p.OrderStatusHistories)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("order_status_history_order_id_fkey");
        });

        modelBuilder.Entity<OrderSummary>(entity =>
        {
            entity.HasNoKey().ToView("order_summary", "sales");

            entity
                .Property(e => e.Customer)
                .HasComment("订单客户姓名，由姓氏和名字拼接。")
                .HasColumnName("customer");
            entity
                .Property(e => e.Discount)
                .HasPrecision(12, 2)
                .HasComment("订单优惠总额，对应 sales.orders.discount_total。")
                .HasColumnName("discount");
            entity
                .Property(e => e.GrandTotal)
                .HasPrecision(12, 2)
                .HasComment("订单应付总额，等于商品小计减优惠总额加税额加配送费。")
                .HasColumnName("grand_total");
            entity
                .Property(e => e.Id)
                .HasComment("订单内部主键，来自 sales.orders.id。")
                .HasColumnName("id");
            entity
                .Property(e => e.ItemCount)
                .HasComment("订单内商品总件数，等于明细 quantity 之和，不是明细行数。")
                .HasColumnName("item_count");
            entity
                .Property(e => e.OrderNumber)
                .HasMaxLength(40)
                .HasComment("唯一的可读订单编号，供客户查询和业务对账使用。")
                .HasColumnName("order_number");
            entity
                .Property(e => e.PlacedAt)
                .HasComment("客户下单时间。")
                .HasColumnName("placed_at");
            entity
                .Property(e => e.PublicId)
                .HasComment("订单对外 UUID 标识，来自 sales.orders.public_id。")
                .HasColumnName("public_id");
            entity
                .Property(e => e.Shipping)
                .HasPrecision(12, 2)
                .HasComment("订单配送费用，对应 sales.orders.shipping_total。")
                .HasColumnName("shipping");
            entity
                .Property(e => e.Status)
                .HasMaxLength(16)
                .HasComment("订单当前状态；含义与 sales.orders.status 相同。")
                .HasColumnName("status");
            entity
                .Property(e => e.Subtotal)
                .HasPrecision(12, 2)
                .HasComment("折扣前税前商品金额，等于所有订单项数量乘单价之和。")
                .HasColumnName("subtotal");
            entity
                .Property(e => e.Tax)
                .HasPrecision(12, 2)
                .HasComment("订单商品税额总计，对应 sales.orders.tax_total。")
                .HasColumnName("tax");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("payments_pkey");

            entity.ToTable(
                "payments",
                "payment",
                tb =>
                    tb.HasComment(
                        "订单支付尝试：允许失败重试产生多条记录，记录支付渠道、金额及授权和扣款时间。"
                    )
            );

            entity.HasIndex(e => e.OrderId, "payments_order_idx");

            entity
                .HasIndex(e => e.ProviderTransactionId, "payments_provider_transaction_id_key")
                .IsUnique();

            entity.HasIndex(e => e.PublicId, "payments_public_id_key").IsUnique();

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.Amount)
                .HasPrecision(12, 2)
                .HasComment(
                    "本次支付尝试的金额；当前业务采用整单支付，应与订单应付总额及币种一致；退款后保留原金额。"
                )
                .HasColumnName("amount");
            entity
                .Property(e => e.AuthorizedAt)
                .HasComment("支付授权成功时间；扣款前必须已有授权时间。")
                .HasColumnName("authorized_at");
            entity
                .Property(e => e.CapturedAt)
                .HasComment("支付实际扣款成功时间；退款及部分退款后仍保留。")
                .HasColumnName("captured_at");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.Currency)
                .HasMaxLength(3)
                .HasDefaultValueSql("'JPY'::bpchar")
                .IsFixedLength()
                .HasComment(
                    "三位货币代码，例如 JPY（日元）或 USD（美元）；金额必须按币种分别计算。"
                )
                .HasColumnName("currency");
            entity
                .Property(e => e.FailedAt)
                .HasComment("支付失败时间。")
                .HasColumnName("failed_at");
            entity
                .Property(e => e.Method)
                .HasMaxLength(24)
                .HasComment(
                    "支付方式：credit_card 信用卡、paypal 贝宝、paypay 电子支付、bank_transfer 银行转账。"
                )
                .HasColumnName("method");
            entity
                .Property(e => e.OrderId)
                .HasComment("订单内部主键，关联 sales.orders.id。")
                .HasColumnName("order_id");
            entity
                .Property(e => e.Provider)
                .HasMaxLength(24)
                .HasComment(
                    "支付服务商：stripe、paypal、paypay 或 card_gateway（银行卡支付网关）。"
                )
                .HasColumnName("provider");
            entity
                .Property(e => e.ProviderTransactionId)
                .HasMaxLength(120)
                .HasComment("支付服务商交易编号；非空时全表唯一，用于对账及回调去重。")
                .HasColumnName("provider_transaction_id");
            entity
                .Property(e => e.PublicId)
                .HasDefaultValueSql("uuidv7()")
                .HasComment(
                    "对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。"
                )
                .HasColumnName("public_id");
            entity
                .Property(e => e.Status)
                .HasMaxLength(24)
                .HasComment(
                    "支付状态：pending 待处理、authorized 已授权、captured 已扣款、failed 失败、cancelled 已取消、refunded 已全额退款、partially_refunded 已部分退款。"
                )
                .HasColumnName("status");

            entity
                .HasOne(d => d.Order)
                .WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("payments_order_id_fkey");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("products_pkey");

            entity.ToTable(
                "products",
                "catalog",
                tb =>
                    tb.HasComment(
                        "商品展示主体：保存品牌、名称、介绍及上下架状态；实际售卖规格见商品变体。"
                    )
            );

            entity.HasIndex(e => e.BrandId, "products_brand_idx");

            entity.HasIndex(e => new { e.CreatedAt, e.Id }, "products_created_idx").IsDescending();

            entity.HasIndex(e => e.PublicId, "products_public_id_key").IsUnique();

            entity.HasIndex(e => e.Slug, "products_slug_key").IsUnique();

            entity
                .HasIndex(
                    e => new
                    {
                        e.Status,
                        e.CreatedAt,
                        e.Id,
                    },
                    "products_status_created_idx"
                )
                .IsDescending(false, true, true);

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.BasePrice)
                .HasPrecision(12, 2)
                .HasComment("商品税前基础展示价格；实际下单单价来自所选变体。")
                .HasColumnName("base_price");
            entity
                .Property(e => e.BrandId)
                .HasComment("品牌内部主键，关联 catalog.brands.id。")
                .HasColumnName("brand_id");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.Currency)
                .HasMaxLength(3)
                .HasDefaultValueSql("'JPY'::bpchar")
                .IsFixedLength()
                .HasComment(
                    "三位货币代码，例如 JPY（日元）或 USD（美元）；金额必须按币种分别计算。"
                )
                .HasColumnName("currency");
            entity
                .Property(e => e.DeletedAt)
                .HasComment("软删除时间；为空表示未软删除。")
                .HasColumnName("deleted_at");
            entity
                .Property(e => e.Description)
                .HasComment("商品详细介绍，可为空。")
                .HasColumnName("description");
            entity
                .Property(e => e.Name)
                .HasMaxLength(200)
                .HasComment("商品当前显示名称。")
                .HasColumnName("name");
            entity
                .Property(e => e.PublicId)
                .HasDefaultValueSql("uuidv7()")
                .HasComment(
                    "对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。"
                )
                .HasColumnName("public_id");
            entity
                .Property(e => e.PublishedAt)
                .HasComment("商品发布时间；为空表示尚未发布。")
                .HasColumnName("published_at");
            entity
                .Property(e => e.Slug)
                .HasMaxLength(240)
                .HasComment("用于页面地址或 API 查询的可读唯一标识。")
                .HasColumnName("slug");
            entity
                .Property(e => e.Status)
                .HasMaxLength(16)
                .HasComment("商品状态：draft 草稿、active 上架、inactive 下架、archived 已归档。")
                .HasColumnName("status");
            entity
                .Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录最后更新时间，由写入方显式维护。")
                .HasColumnName("updated_at");

            entity
                .HasOne(d => d.Brand)
                .WithMany(p => p.Products)
                .HasForeignKey(d => d.BrandId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("products_brand_id_fkey");

            entity
                .HasMany(d => d.Categories)
                .WithMany(p => p.Products)
                .UsingEntity<Dictionary<string, object>>(
                    "ProductCategory",
                    r =>
                        r.HasOne<Category>()
                            .WithMany()
                            .HasForeignKey("CategoryId")
                            .OnDelete(DeleteBehavior.Restrict)
                            .HasConstraintName("product_categories_category_id_fkey"),
                    l =>
                        l.HasOne<Product>()
                            .WithMany()
                            .HasForeignKey("ProductId")
                            .HasConstraintName("product_categories_product_id_fkey"),
                    j =>
                    {
                        j.HasKey("ProductId", "CategoryId").HasName("product_categories_pkey");
                        j.ToTable(
                            "product_categories",
                            "catalog",
                            tb =>
                                tb.HasComment(
                                    "商品与分类的多对多关联；同一商品和分类组合不可重复。"
                                )
                        );
                        j.HasIndex(
                            new[] { "CategoryId", "ProductId" },
                            "product_categories_category_idx"
                        );
                        j.IndexerProperty<long>("ProductId")
                            .HasComment("商品内部主键，关联 catalog.products.id。")
                            .HasColumnName("product_id");
                        j.IndexerProperty<long>("CategoryId")
                            .HasComment("分类内部主键，关联 catalog.categories.id。")
                            .HasColumnName("category_id");
                    }
                );
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("product_images_pkey");

            entity.ToTable(
                "product_images",
                "catalog",
                tb => tb.HasComment("商品图片：可关联具体变体；不关联变体时作为商品通用图片。")
            );

            entity
                .HasIndex(e => e.ProductId, "product_images_primary_uq")
                .IsUnique()
                .HasFilter("is_primary");

            entity.HasIndex(e => new { e.ProductId, e.SortOrder }, "product_images_product_idx");

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.AltText)
                .HasComment("图片替代文字，供图片无法加载或辅助阅读时使用。")
                .HasColumnName("alt_text");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.IsPrimary)
                .HasComment("是否为商品主图。")
                .HasColumnName("is_primary");
            entity
                .Property(e => e.ProductId)
                .HasComment("商品内部主键，关联 catalog.products.id。")
                .HasColumnName("product_id");
            entity
                .Property(e => e.SortOrder)
                .HasComment("显示排序值，通常按升序展示。")
                .HasColumnName("sort_order");
            entity
                .Property(e => e.Url)
                .HasComment("图片资源地址；种子数据使用示例地址。")
                .HasColumnName("url");
            entity
                .Property(e => e.VariantId)
                .HasComment("可选的变体内部主键；为空表示商品通用图片，非空时必须属于同一商品。")
                .HasColumnName("variant_id");

            entity
                .HasOne(d => d.Product)
                .WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("product_images_product_id_fkey");

            entity
                .HasOne(d => d.ProductVariant)
                .WithMany(p => p.ProductImages)
                .HasPrincipalKey(p => new { p.Id, p.ProductId })
                .HasForeignKey(d => new { d.VariantId, d.ProductId })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("product_images_variant_id_product_id_fkey");
        });

        modelBuilder.Entity<ProductInventorySummary>(entity =>
        {
            entity.HasNoKey().ToView("product_inventory_summary", "catalog");

            entity
                .Property(e => e.AvailableStock)
                .HasComment("该变体跨仓库的可售库存，等于实物库存总量减预占库存总量。")
                .HasColumnName("available_stock");
            entity
                .Property(e => e.Product)
                .HasMaxLength(200)
                .HasComment("当前商品名称。")
                .HasColumnName("product");
            entity
                .Property(e => e.ProductId)
                .HasComment("商品内部主键，关联 catalog.products.id。")
                .HasColumnName("product_id");
            entity
                .Property(e => e.ReservedStock)
                .HasComment("该变体跨仓库的预占库存总量。")
                .HasColumnName("reserved_stock");
            entity
                .Property(e => e.Sku)
                .HasMaxLength(64)
                .HasComment("库存管理编码，唯一标识一个商品变体。")
                .HasColumnName("sku");
            entity
                .Property(e => e.TotalStock)
                .HasComment("该变体跨仓库的实物库存总量。")
                .HasColumnName("total_stock");
            entity
                .Property(e => e.Variant)
                .HasMaxLength(120)
                .HasComment("当前商品变体名称。")
                .HasColumnName("variant");
            entity
                .Property(e => e.VariantId)
                .HasComment("商品变体内部主键，关联 catalog.product_variants.id。")
                .HasColumnName("variant_id");
        });

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("product_reviews_pkey");

            entity.ToTable(
                "product_reviews",
                "review",
                tb =>
                    tb.HasComment(
                        "商品评价：保存评分、文字与审核状态；已验证购买评价须关联真实购买明细及收货时间。"
                    )
            );

            entity.HasIndex(e => e.OrderItemId, "product_reviews_order_item_id_key").IsUnique();

            entity.HasIndex(e => e.PublicId, "product_reviews_public_id_key").IsUnique();

            entity.HasIndex(e => e.CustomerId, "reviews_customer_idx");

            entity
                .HasIndex(
                    e => new
                    {
                        e.ProductId,
                        e.CreatedAt,
                        e.Id,
                    },
                    "reviews_product_created_idx"
                )
                .IsDescending(false, true, true);

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.Content)
                .HasComment("评价文字内容，可为空，允许仅提交评分。")
                .HasColumnName("content");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.CustomerId)
                .HasComment("所属客户的内部主键，关联 account.customers.id。")
                .HasColumnName("customer_id");
            entity
                .Property(e => e.IsVerifiedPurchase)
                .HasComment(
                    "是否为已验证购买评价；为真时须关联同客户同商品的已收货订单项，由触发器校验。"
                )
                .HasColumnName("is_verified_purchase");
            entity
                .Property(e => e.OrderItemId)
                .HasComment(
                    "购买明细内部主键，关联 sales.order_items.id；非空时唯一，限制同一订单项重复评价。"
                )
                .HasColumnName("order_item_id");
            entity
                .Property(e => e.ProductId)
                .HasComment("商品内部主键，关联 catalog.products.id。")
                .HasColumnName("product_id");
            entity
                .Property(e => e.PublicId)
                .HasDefaultValueSql("uuidv7()")
                .HasComment(
                    "对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。"
                )
                .HasColumnName("public_id");
            entity
                .Property(e => e.Rating)
                .HasComment("商品评分，取值为一至五星。")
                .HasColumnName("rating");
            entity
                .Property(e => e.Status)
                .HasMaxLength(16)
                .HasComment("评价审核状态：pending 待审核、published 已发布、rejected 已拒绝。")
                .HasColumnName("status");
            entity
                .Property(e => e.Title)
                .HasMaxLength(160)
                .HasComment("评价标题，可为空。")
                .HasColumnName("title");
            entity
                .Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录最后更新时间，由写入方显式维护。")
                .HasColumnName("updated_at");

            entity
                .HasOne(d => d.Customer)
                .WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("product_reviews_customer_id_fkey");

            entity
                .HasOne(d => d.OrderItem)
                .WithOne(p => p.ProductReview)
                .HasForeignKey<ProductReview>(d => d.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("product_reviews_order_item_id_fkey");

            entity
                .HasOne(d => d.Product)
                .WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("product_reviews_product_id_fkey");
        });

        modelBuilder.Entity<ProductSalesSummary>(entity =>
        {
            entity.HasNoKey().ToView("product_sales_summary", "catalog");

            entity
                .Property(e => e.GrossSales)
                .HasComment(
                    "已付款订单的折扣前税前商品销售额，等于成交单价乘数量之和；不减退款，不含税费和运费。"
                )
                .HasColumnName("gross_sales");
            entity
                .Property(e => e.OrderCount)
                .HasComment("包含该商品的已付款订单去重数量，包括后续退货订单。")
                .HasColumnName("order_count");
            entity
                .Property(e => e.Product)
                .HasMaxLength(200)
                .HasComment("当前商品名称。")
                .HasColumnName("product");
            entity
                .Property(e => e.ProductId)
                .HasComment("商品内部主键，关联 catalog.products.id。")
                .HasColumnName("product_id");
            entity
                .Property(e => e.TotalQuantitySold)
                .HasComment("已付款订单的商品总销售件数，包括后续退货订单，不扣除退货件数。")
                .HasColumnName("total_quantity_sold");
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("product_variants_pkey");

            entity.ToTable(
                "product_variants",
                "catalog",
                tb =>
                    tb.HasComment(
                        "商品变体：每行代表一个可售规格及其唯一 SKU，保存规格售价与属性。"
                    )
            );

            entity.HasIndex(e => e.Barcode, "product_variants_barcode_key").IsUnique();

            entity
                .HasIndex(e => new { e.Id, e.ProductId }, "product_variants_id_product_id_key")
                .IsUnique();

            entity.HasIndex(e => e.PublicId, "product_variants_public_id_key").IsUnique();

            entity.HasIndex(e => e.Sku, "product_variants_sku_key").IsUnique();

            entity.HasIndex(e => e.ProductId, "variants_product_idx");

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.Attributes)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasComment("变体规格属性的 JSON 对象，例如颜色、尺码或容量；核心关联仍使用外键。")
                .HasColumnType("jsonb")
                .HasColumnName("attributes");
            entity
                .Property(e => e.Barcode)
                .HasMaxLength(32)
                .HasComment("商品条码；非空时必须唯一。")
                .HasColumnName("barcode");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.Currency)
                .HasMaxLength(3)
                .HasDefaultValueSql("'JPY'::bpchar")
                .IsFixedLength()
                .HasComment(
                    "三位货币代码，例如 JPY（日元）或 USD（美元）；金额必须按币种分别计算。"
                )
                .HasColumnName("currency");
            entity
                .Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasComment("该变体是否启用；可售性还需结合商品状态和可售库存判断。")
                .HasColumnName("is_active");
            entity
                .Property(e => e.Name)
                .HasMaxLength(120)
                .HasComment("变体规格显示名称，例如黑色／512GB。")
                .HasColumnName("name");
            entity
                .Property(e => e.Price)
                .HasPrecision(12, 2)
                .HasComment("商品变体税前售价，非负，币种由 currency 指定。")
                .HasColumnName("price");
            entity
                .Property(e => e.ProductId)
                .HasComment("商品内部主键，关联 catalog.products.id。")
                .HasColumnName("product_id");
            entity
                .Property(e => e.PublicId)
                .HasDefaultValueSql("uuidv7()")
                .HasComment(
                    "对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。"
                )
                .HasColumnName("public_id");
            entity
                .Property(e => e.Sku)
                .HasMaxLength(64)
                .HasComment("库存管理编码，唯一标识一个商品变体。")
                .HasColumnName("sku");
            entity
                .Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录最后更新时间，由写入方显式维护。")
                .HasColumnName("updated_at");
            entity
                .Property(e => e.WeightGrams)
                .HasComment("商品变体重量，单位为克，必须非负。")
                .HasColumnName("weight_grams");

            entity
                .HasOne(d => d.Product)
                .WithMany(p => p.ProductVariants)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("product_variants_product_id_fkey");
        });

        modelBuilder.Entity<Refund>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("refunds_pkey");

            entity.ToTable(
                "refunds",
                "payment",
                tb =>
                    tb.HasComment(
                        "支付退款记录：待处理与已完成退款合计不得超过对应已扣款支付金额。"
                    )
            );

            entity.HasIndex(e => e.PaymentId, "refunds_payment_idx");

            entity.HasIndex(e => e.ProviderRefundId, "refunds_provider_refund_id_key").IsUnique();

            entity.HasIndex(e => e.PublicId, "refunds_public_id_key").IsUnique();

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.Amount)
                .HasPrecision(12, 2)
                .HasComment(
                    "本次退款金额，必须大于零；使用原支付币种，待处理与已完成退款合计不能超过已扣款金额。"
                )
                .HasColumnName("amount");
            entity
                .Property(e => e.CompletedAt)
                .HasComment("退款完成时间；仅 completed 状态非空。")
                .HasColumnName("completed_at");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.PaymentId)
                .HasComment("支付记录内部主键，关联 payment.payments.id。")
                .HasColumnName("payment_id");
            entity
                .Property(e => e.ProviderRefundId)
                .HasMaxLength(120)
                .HasComment("支付服务商退款编号；非空时全表唯一，用于退款对账。")
                .HasColumnName("provider_refund_id");
            entity
                .Property(e => e.PublicId)
                .HasDefaultValueSql("uuidv7()")
                .HasComment(
                    "对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。"
                )
                .HasColumnName("public_id");
            entity
                .Property(e => e.Reason)
                .HasComment("退款原因，不能为空值。")
                .HasColumnName("reason");
            entity
                .Property(e => e.Status)
                .HasMaxLength(16)
                .HasComment("退款状态：pending 待处理、completed 已完成、failed 失败。")
                .HasColumnName("status");

            entity
                .HasOne(d => d.Payment)
                .WithMany(p => p.Refunds)
                .HasForeignKey(d => d.PaymentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("refunds_payment_id_fkey");
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shipments_pkey");

            entity.ToTable(
                "shipments",
                "shipping",
                tb =>
                    tb.HasComment(
                        "订单物流记录：保存发货仓、承运商、运单号与配送时间；订单可拆成多个包裹。"
                    )
            );

            entity.HasIndex(e => e.OrderId, "shipments_order_idx");

            entity.HasIndex(e => e.PublicId, "shipments_public_id_key").IsUnique();

            entity.HasIndex(e => e.TrackingNumber, "shipments_tracking_number_key").IsUnique();

            entity.HasIndex(e => e.WarehouseId, "shipments_warehouse_idx");

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.Carrier)
                .HasMaxLength(40)
                .HasComment("承运商名称，例如 Yamato、Sagawa 或 Japan Post。")
                .HasColumnName("carrier");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.DeliveredAt)
                .HasComment("签收或送达时间，不得早于发货时间。")
                .HasColumnName("delivered_at");
            entity
                .Property(e => e.OrderId)
                .HasComment("订单内部主键，关联 sales.orders.id。")
                .HasColumnName("order_id");
            entity
                .Property(e => e.PublicId)
                .HasDefaultValueSql("uuidv7()")
                .HasComment(
                    "对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。"
                )
                .HasColumnName("public_id");
            entity
                .Property(e => e.ShippedAt)
                .HasComment("实际发货时间。")
                .HasColumnName("shipped_at");
            entity
                .Property(e => e.Status)
                .HasMaxLength(16)
                .HasComment(
                    "物流状态：pending 待处理、ready 待发货、shipped 已发货、in_transit 运输中、delivered 已送达、failed 配送失败、returned 已退回。"
                )
                .HasColumnName("status");
            entity
                .Property(e => e.TrackingNumber)
                .HasMaxLength(80)
                .HasComment("物流运单号；非空时全表唯一。")
                .HasColumnName("tracking_number");
            entity
                .Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录最后更新时间，由写入方显式维护。")
                .HasColumnName("updated_at");
            entity
                .Property(e => e.WarehouseId)
                .HasComment("仓库内部主键，关联 inventory.warehouses.id。")
                .HasColumnName("warehouse_id");

            entity
                .HasOne(d => d.Order)
                .WithMany(p => p.Shipments)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("shipments_order_id_fkey");

            entity
                .HasOne(d => d.Warehouse)
                .WithMany(p => p.Shipments)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("shipments_warehouse_id_fkey");
        });

        modelBuilder.Entity<Stock>(entity =>
        {
            entity.HasKey(e => new { e.WarehouseId, e.VariantId }).HasName("stocks_pkey");

            entity.ToTable(
                "stocks",
                "inventory",
                tb => tb.HasComment("仓库与商品变体的当前库存；可售库存等于实物库存减预占库存。")
            );

            entity.HasIndex(e => e.VariantId, "stocks_variant_idx");

            entity
                .Property(e => e.WarehouseId)
                .HasComment("仓库内部主键，关联 inventory.warehouses.id。")
                .HasColumnName("warehouse_id");
            entity
                .Property(e => e.VariantId)
                .HasComment("商品变体内部主键，关联 catalog.product_variants.id。")
                .HasColumnName("variant_id");
            entity
                .Property(e => e.QuantityOnHand)
                .HasComment("仓库实际持有数量，包含已预占部分，必须非负。")
                .HasColumnName("quantity_on_hand");
            entity
                .Property(e => e.QuantityReserved)
                .HasComment("已预占但尚未出库的数量，介于零和实物库存之间。")
                .HasColumnName("quantity_reserved");
            entity
                .Property(e => e.ReorderLevel)
                .HasDefaultValue(10)
                .HasComment("补货预警阈值，单位为件，必须非负。")
                .HasColumnName("reorder_level");
            entity
                .Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录最后更新时间，由写入方显式维护。")
                .HasColumnName("updated_at");

            entity
                .HasOne(d => d.Variant)
                .WithMany(p => p.Stocks)
                .HasForeignKey(d => d.VariantId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("stocks_variant_id_fkey");

            entity
                .HasOne(d => d.Warehouse)
                .WithMany(p => p.Stocks)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("stocks_warehouse_id_fkey");
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("stock_movements_pkey");

            entity.ToTable(
                "stock_movements",
                "inventory",
                tb =>
                    tb.HasComment(
                        "库存变动流水：进货、销售、退货、调整影响实物库存，预占和释放影响预占库存。"
                    )
            );

            entity
                .HasIndex(e => e.OrderId, "stock_movements_order_idx")
                .HasFilter("(order_id IS NOT NULL)");

            entity.HasIndex(
                e => new
                {
                    e.VariantId,
                    e.CreatedAt,
                    e.Id,
                },
                "stock_movements_variant_time_idx"
            );

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("库存变动发生时间，按此时间及流水主键重建库存余额。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.MovementType)
                .HasMaxLength(16)
                .HasComment(
                    "变动类型：purchase 进货、sale 销售出库、return 退货入库、adjustment 库存调整、reservation 预占、release 释放预占。"
                )
                .HasColumnName("movement_type");
            entity.Property(e => e.Note).HasComment("补充说明或操作备注。").HasColumnName("note");
            entity
                .Property(e => e.OrderId)
                .HasComputedColumnSql(
                    "\nCASE\n    WHEN ((reference_type)::text = 'order'::text) THEN reference_id\n    ELSE NULL::bigint\nEND",
                    true
                )
                .HasComment(
                    "生成列：来源类型为 order 时取 reference_id，否则为空；外键校验来源订单存在，不可手动写入。"
                )
                .HasColumnName("order_id");
            entity
                .Property(e => e.Quantity)
                .HasComment(
                    "本次变动数量，不得为零；进货、退货、预占为正，销售、释放为负，调整可正可负；预占和释放仅改变预占库存。"
                )
                .HasColumnName("quantity");
            entity
                .Property(e => e.ReferenceId)
                .HasComment(
                    "来源记录标识；来源为订单时必须填写订单内部主键；采购及调整尚未建立对应业务表。"
                )
                .HasColumnName("reference_id");
            entity
                .Property(e => e.ReferenceType)
                .HasMaxLength(16)
                .HasComment("来源类型：order 订单、purchase 采购、adjustment 库存调整。")
                .HasColumnName("reference_type");
            entity
                .Property(e => e.VariantId)
                .HasComment("商品变体内部主键，关联 catalog.product_variants.id。")
                .HasColumnName("variant_id");
            entity
                .Property(e => e.WarehouseId)
                .HasComment("仓库内部主键，关联 inventory.warehouses.id。")
                .HasColumnName("warehouse_id");

            entity
                .HasOne(d => d.Order)
                .WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("stock_movements_order_id_fkey");

            entity
                .HasOne(d => d.Stock)
                .WithMany(p => p.StockMovements)
                .HasForeignKey(d => new { d.WarehouseId, d.VariantId })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("stock_movements_warehouse_id_variant_id_fkey");
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("warehouses_pkey");

            entity.ToTable(
                "warehouses",
                "inventory",
                tb => tb.HasComment("仓库资料：保存仓库编码、名称及日本仓库地址。")
            );

            entity.HasIndex(e => e.Code, "warehouses_code_key").IsUnique();

            entity.HasIndex(e => e.PublicId, "warehouses_public_id_key").IsUnique();

            entity
                .Property(e => e.Id)
                .HasComment("内部主键，使用 bigint 自增标识列，供表间关联使用。")
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity
                .Property(e => e.Address)
                .HasMaxLength(200)
                .HasComment("仓库详细地址。")
                .HasColumnName("address");
            entity
                .Property(e => e.City)
                .HasMaxLength(80)
                .HasComment("市、区、町或村名称。")
                .HasColumnName("city");
            entity
                .Property(e => e.Code)
                .HasMaxLength(32)
                .HasComment("唯一仓库编码，供库存与发货业务引用。")
                .HasColumnName("code");
            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("记录创建时间，使用带时区时间戳。")
                .HasColumnName("created_at");
            entity
                .Property(e => e.Name)
                .HasMaxLength(120)
                .HasComment("仓库显示名称。")
                .HasColumnName("name");
            entity
                .Property(e => e.PostalCode)
                .HasMaxLength(12)
                .HasComment("邮政编码；日本地址通常采用三位数字加连字符加四位数字。")
                .HasColumnName("postal_code");
            entity
                .Property(e => e.Prefecture)
                .HasMaxLength(80)
                .HasComment("都道府县名称，例如东京都或大阪府。")
                .HasColumnName("prefecture");
            entity
                .Property(e => e.PublicId)
                .HasDefaultValueSql("uuidv7()")
                .HasComment(
                    "对外公开的 UUID 标识，供 API 使用；支持 uuidv7() 时默认生成第七版，否则生成随机 UUID。"
                )
                .HasColumnName("public_id");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
