using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;

namespace MiniStore.Features.Customers;

/// <summary>处理客户与地址功能的数据查询与业务规则。同一次请求共用注入的 DbContext，事务边界保留在业务方法内。</summary>
public sealed class CustomerService(StoreDbContext db)
{
    /// <summary>读取当前登录客户的资料，只返回允许公开给本人的字段，不返回密码哈希。</summary>
    public async Task<CustomerProfileDto> GetProfileAsync(long customerId, CancellationToken ct)
    {
        return await db
            .Customers.AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => new CustomerProfileDto(
                c.PublicId,
                c.Email,
                c.FirstName,
                c.LastName,
                c.Phone,
                c.BirthDate,
                c.Status,
                c.CreatedAt
            ))
            .SingleAsync(ct);
    }

    /// <summary>更新姓名、电话与出生日期；customerId 来自认证结果，不能由请求体冒充他人。</summary>
    public async Task UpdateProfileAsync(ProfileRequest r, long customerId, CancellationToken ct)
    {
        Rules.Require(
            r.Phone?.Length is not > 24
                && (
                    !r.BirthDate.HasValue
                    || r.BirthDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow)
                ),
            "电话或出生日期无效。"
        );
        var c = await db.Customers.SingleAsync(c => c.Id == customerId, ct);
        c.FirstName = Rules.Text(r.FirstName, 80, "名字");
        c.LastName = Rules.Text(r.LastName, 80, "姓氏");
        c.Phone = r.Phone;
        c.BirthDate = r.BirthDate;
        c.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>列出本人的地址，默认地址优先；数据库内部地址 ID 不暴露给客户端。</summary>
    public async Task<IEnumerable<AddressDto>> ListAddressesAsync(
        long customerId,
        CancellationToken ct
    )
    {
        return (
            await db
                .CustomerAddresses.AsNoTracking()
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.Id)
                .ToListAsync(ct)
        ).Select(ToDto);
    }

    /// <summary>新增自己的地址；与编辑共用 SaveAddress，统一处理地址校验和默认地址规则。</summary>
    public async Task<AddressDto> CreateAddressAsync(
        AddressRequest r,
        long customerId,
        CancellationToken ct
    )
    {
        return await SaveAddress(null, r, customerId, db, ct);
    }

    /// <summary>按公开 UUID 编辑本人地址；所属客户和已有地址类型不可通过请求改变。</summary>
    public async Task<AddressDto> UpdateAddressAsync(
        Guid id,
        AddressRequest r,
        long customerId,
        CancellationToken ct
    )
    {
        return await SaveAddress(id, r, customerId, db, ct);
    }

    /// <summary>事务内删除本人地址；删除默认地址后，为同类型剩余地址补选默认项。</summary>
    public async Task DeleteAddressAsync(Guid id, long customerId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RowLocks.Customer(db, customerId, ct);
        var address =
            await db.CustomerAddresses.SingleOrDefaultAsync(
                a => a.PublicId == id && a.CustomerId == customerId,
                ct
            ) ?? throw ApiError.NotFound();
        db.Remove(address);
        await db.SaveChangesAsync(ct);
        if (address.IsDefault)
        {
            var next = await db
                .CustomerAddresses.Where(a =>
                    a.CustomerId == address.CustomerId && a.AddressType == address.AddressType
                )
                .OrderBy(a => a.Id)
                .FirstOrDefaultAsync(ct);
            if (next != null)
            {
                next.IsDefault = true;
                next.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
        await tx.CommitAsync(ct);
    }

    /// <summary>后台分页检索客户，汇总订单数和已付款 JPY 订单金额；累计金额尚未扣除退款。</summary>
    public async Task<PageResult<CustomerSummaryDto>> ListAsync(ListQuery q, CancellationToken ct)
    {
        q.Validate();
        var source = db.Customers.AsNoTracking();
        if (q.Status != null)
            source = source.Where(c => c.Status == q.Status);
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var pattern = "%" + q.Q.Trim() + "%";
            source = source.Where(c =>
                EF.Functions.ILike(c.Email, pattern)
                || EF.Functions.ILike(c.LastName + c.FirstName, pattern)
            );
        }
        return await source
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Select(c => new CustomerSummaryDto(
                c.PublicId,
                c.Email,
                c.LastName + " " + c.FirstName,
                c.Phone,
                c.Status,
                c.CreatedAt,
                c.Orders.Count,
                c.Orders.Where(o => o.PaidAt != null && o.Currency == "JPY").Sum(o => o.GrandTotal)
            ))
            .PageAsync(q, ct);
    }

    /// <summary>后台读取客户资料、地址及最近 20 笔订单；不是完整订单历史的分页入口。</summary>
    public async Task<CustomerDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var c =
            await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.PublicId == id, ct)
            ?? throw ApiError.NotFound();
        return new CustomerDetailDto(
            c.PublicId,
            c.Email,
            c.FirstName,
            c.LastName,
            c.Status,
            c.CreatedAt,
            (
                await db
                    .CustomerAddresses.AsNoTracking()
                    .Where(a => a.CustomerId == c.Id)
                    .ToListAsync(ct)
            ).Select(ToDto),
            await db
                .Orders.AsNoTracking()
                .Where(o => o.CustomerId == c.Id)
                .OrderByDescending(o => o.CreatedAt)
                .ThenByDescending(o => o.Id)
                .Take(20)
                .Select(o => new CustomerOrderDto(
                    o.PublicId,
                    o.OrderNumber,
                    o.Status,
                    o.GrandTotal,
                    o.Currency,
                    o.PlacedAt
                ))
                .ToListAsync(ct)
        );
    }

    /// <summary>后台启用或停用客户；认证中间件会在后续请求重新检查状态。</summary>
    public async Task SetStatusAsync(Guid id, CustomerStatusRequest r, CancellationToken ct)
    {
        Rules.Require(r.Status is "active" or "disabled", "只支持启用或停用客户。");
        var c =
            await db.Customers.SingleOrDefaultAsync(c => c.PublicId == id, ct)
            ?? throw ApiError.NotFound();
        c.Status = r.Status;
        c.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>将地址实体转换为接口数据，避免导航属性被序列化为循环关系。</summary>
    private static AddressDto ToDto(CustomerAddress a) =>
        new(
            a.PublicId,
            a.AddressType,
            a.RecipientName,
            a.PostalCode,
            a.CountryCode,
            a.Prefecture,
            a.City,
            a.AddressLine1,
            a.AddressLine2,
            a.Phone,
            a.IsDefault
        );

    /// <summary>锁定客户行后维护地址与默认项，在一个事务内满足每客户、每类型的默认地址约束。</summary>
    private static async Task<AddressDto> SaveAddress(
        Guid? id,
        AddressRequest r,
        long customerId,
        StoreDbContext db,
        CancellationToken ct
    )
    {
        Rules.Require(
            r.AddressType is "shipping" or "billing" && r.CountryCode == "JP",
            "当前仅支持日本的收货或账单地址。"
        );
        Rules.Require(
            Regex.IsMatch(r.PostalCode ?? "", "^[0-9]{3}-?[0-9]{4}$")
                && Regex.IsMatch(r.Phone ?? "", "^[+0-9 ()-]{8,24}$"),
            "日本邮编或联系电话格式不正确。"
        );
        Rules.Require(r.AddressLine2?.Length is not > 160, "楼名和房间号过长。");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RowLocks.Customer(db, customerId, ct);
        var a = id.HasValue
            ? await db.CustomerAddresses.SingleOrDefaultAsync(
                a => a.PublicId == id && a.CustomerId == customerId,
                ct
            ) ?? throw ApiError.NotFound()
            : new CustomerAddress { CustomerId = customerId, CreatedAt = DateTime.UtcNow };
        if (id.HasValue)
            Rules.Require(
                a.AddressType == r.AddressType,
                "现有地址类型不可更改，请新增另一类型地址。"
            );
        else
            Rules.Require(
                await db.CustomerAddresses.CountAsync(a => a.CustomerId == customerId, ct) < 20,
                "最多保存 20 个地址。"
            );
        var makeDefault =
            r.IsDefault
            || !await db.CustomerAddresses.AnyAsync(
                x => x.CustomerId == customerId && x.AddressType == r.AddressType && x.Id != a.Id,
                ct
            );
        if (a.IsDefault && !makeDefault)
            throw new ApiError(400, "default_address", "请先将另一个地址设为默认。");
        if (makeDefault)
            await db
                .CustomerAddresses.Where(x =>
                    x.CustomerId == customerId && x.AddressType == r.AddressType && x.IsDefault
                )
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.IsDefault, false)
                            .SetProperty(x => x.UpdatedAt, DateTime.UtcNow),
                    ct
                );
        a.AddressType = r.AddressType;
        a.RecipientName = Rules.Text(r.RecipientName, 160, "收件人");
        a.PostalCode = r.PostalCode!;
        a.CountryCode = "JP";
        a.Prefecture = Rules.Text(r.Prefecture, 80, "都道府县");
        a.City = Rules.Text(r.City, 80, "城市");
        a.AddressLine1 = Rules.Text(r.AddressLine1, 160, "详细地址");
        a.AddressLine2 = r.AddressLine2;
        a.Phone = r.Phone;
        a.IsDefault = makeDefault;
        a.UpdatedAt = DateTime.UtcNow;
        if (!id.HasValue)
            db.Add(a);
        else
            db.Entry(a).Property(x => x.IsDefault).IsModified = true;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(a);
    }
}
