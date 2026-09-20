namespace MiniStore.Features.Marketing;

// DTO 定义接口输入输出，与数据库实体分开；对外只传递业务需要的字段。

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <remarks><c>DiscountValue</c>：fixed 时表示减去的整数日元；percentage 时表示百分数，例如 10 表示减 10%。</remarks>
/// <remarks><c>StartsAt</c>：带时区的开始时间；服务写入时转换成 UTC。</remarks>
/// <remarks><c>UsageLimit</c>：允许核销的次数上限；null 表示不设上限。</remarks>
/// <param name="Code">优惠码，3～40 位字母、数字、下划线或连字符，保存为大写且唯一。</param>
/// <param name="Name">名称，必填，长度限制见对应业务校验。</param>
/// <param name="DiscountType">percentage 按百分比优惠，fixed 固定金额优惠。</param>
/// <param name="DiscountValue">正整数；percentage 时不超过 100，10 表示优惠 10%；fixed 时为 JPY。</param>
/// <param name="MinOrderAmount">最低订单门槛，非负整数 JPY，按折扣前税前商品小计判断。</param>
/// <param name="MaxDiscountAmount">最高优惠额，可为空；非空时为正整数 JPY。</param>
/// <param name="UsageLimit">使用次数上限，可为空；非空时须为正整数且不能小于已核销次数。</param>
/// <param name="StartsAt">开始时间，ISO 8601，须带 Z 或时区偏移。</param>
/// <param name="EndsAt">结束时间，须晚于开始时间；已过期券不能设为启用。</param>
/// <param name="IsActive">是否启用。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record CouponRequest(
    string Code,
    string Name,
    string DiscountType,
    decimal DiscountValue,
    decimal MinOrderAmount,
    decimal? MaxDiscountAmount,
    int? UsageLimit,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsActive
);

/// <summary>请求模型：由 ASP.NET Core 将请求 JSON 反序列化为对象，无需手动 new。</summary>
/// <param name="IsActive">是否启用。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "ClassNeverInstantiated.Global",
    Justification = "ASP.NET Core 请求体绑定会自动创建此类型。"
)]
public sealed record CouponStatusRequest(bool IsActive);

/// <summary>优惠券维护信息；IsActive 是启用开关，有效期、门槛和次数仍在结算时校验。</summary>
public sealed record CouponDto(
    Guid Id,
    string Code,
    string Name,
    string DiscountType,
    decimal DiscountValue,
    decimal MinOrderAmount,
    decimal? MaxDiscountAmount,
    int? UsageLimit,
    int UsedCount,
    DateTime StartsAt,
    DateTime EndsAt,
    bool IsActive
);

/// <summary>创建优惠券后返回的公开标识。</summary>
public sealed record CouponCreatedDto(Guid Id);
