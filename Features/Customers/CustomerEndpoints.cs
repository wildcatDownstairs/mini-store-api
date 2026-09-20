using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Data.Entities;

namespace MiniStore.Features.Customers;

public sealed record AddressRequest(
    string AddressType,
    string RecipientName,
    string PostalCode,
    string CountryCode,
    string Prefecture,
    string City,
    string AddressLine1,
    string? AddressLine2,
    string Phone,
    bool IsDefault
);

public sealed record AddressDto(
    Guid Id,
    string AddressType,
    string RecipientName,
    string PostalCode,
    string CountryCode,
    string Prefecture,
    string City,
    string AddressLine1,
    string? AddressLine2,
    string? Phone,
    bool IsDefault
);

public sealed record ProfileRequest(
    string FirstName,
    string LastName,
    string? Phone,
    DateOnly? BirthDate
);

public sealed record CustomerStatusRequest(string Status);

public static class CustomerEndpoints
{
    public static AddressDto ToDto(CustomerAddress a) =>
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

    public static void MapCustomers(this WebApplication app)
    {
        var me = app.MapGroup("/api/me").RequireAuthorization("Customer").WithTags("客户与地址");
        me.MapGet(
            "",
            async (ClaimsPrincipal user, StoreDbContext db, CancellationToken ct) =>
                await db
                    .Customers.AsNoTracking()
                    .Where(c => c.Id == user.ActorId())
                    .Select(c => new
                    {
                        id = c.PublicId,
                        c.Email,
                        c.FirstName,
                        c.LastName,
                        c.Phone,
                        c.BirthDate,
                        c.Status,
                        c.CreatedAt,
                    })
                    .SingleAsync(ct)
        );
        me.MapPut(
            "",
            async (
                ProfileRequest r,
                ClaimsPrincipal user,
                StoreDbContext db,
                CancellationToken ct
            ) =>
            {
                Rules.Require(
                    r.Phone?.Length is not > 24
                        && (
                            !r.BirthDate.HasValue
                            || r.BirthDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow)
                        ),
                    "电话或出生日期无效。"
                );
                var c = await db.Customers.SingleAsync(c => c.Id == user.ActorId(), ct);
                c.FirstName = Rules.Text(r.FirstName, 80, "名字");
                c.LastName = Rules.Text(r.LastName, 80, "姓氏");
                c.Phone = r.Phone;
                c.BirthDate = r.BirthDate;
                c.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
        );
        me.MapGet(
            "/addresses",
            async (ClaimsPrincipal user, StoreDbContext db, CancellationToken ct) =>
                (
                    await db
                        .CustomerAddresses.AsNoTracking()
                        .Where(a => a.CustomerId == user.ActorId())
                        .OrderByDescending(a => a.IsDefault)
                        .ThenBy(a => a.Id)
                        .ToListAsync(ct)
                ).Select(ToDto)
        );
        me.MapPost(
            "/addresses",
            async (
                AddressRequest r,
                ClaimsPrincipal user,
                StoreDbContext db,
                CancellationToken ct
            ) => Results.Ok(await SaveAddress(null, r, user.ActorId(), db, ct))
        );
        me.MapPut(
            "/addresses/{id:guid}",
            (
                Guid id,
                AddressRequest r,
                ClaimsPrincipal user,
                StoreDbContext db,
                CancellationToken ct
            ) => SaveAddress(id, r, user.ActorId(), db, ct)
        );
        me.MapDelete(
            "/addresses/{id:guid}",
            async (Guid id, ClaimsPrincipal user, StoreDbContext db, CancellationToken ct) =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                await RowLocks.Customer(db, user.ActorId(), ct);
                var address =
                    await db.CustomerAddresses.SingleOrDefaultAsync(
                        a => a.PublicId == id && a.CustomerId == user.ActorId(),
                        ct
                    ) ?? throw ApiError.NotFound();
                db.Remove(address);
                await db.SaveChangesAsync(ct);
                if (address.IsDefault)
                {
                    var next = await db
                        .CustomerAddresses.Where(a =>
                            a.CustomerId == address.CustomerId
                            && a.AddressType == address.AddressType
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
                return Results.NoContent();
            }
        );
        var admin = app.MapGroup("/api/admin/customers")
            .RequireAuthorization("AdminRead")
            .WithTags("客户管理");
        admin.MapGet(
            "",
            async (StoreDbContext db, [AsParameters] ListQuery q, CancellationToken ct) =>
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
                    .Select(c => new
                    {
                        id = c.PublicId,
                        c.Email,
                        name = c.LastName + " " + c.FirstName,
                        c.Phone,
                        c.Status,
                        c.CreatedAt,
                        orderCount = c.Orders.Count,
                        totalSpent = c
                            .Orders.Where(o => o.PaidAt != null && o.Currency == "JPY")
                            .Sum(o => o.GrandTotal),
                    })
                    .PageAsync(q, ct);
            }
        );
        admin.MapGet(
            "/{id:guid}",
            async (Guid id, StoreDbContext db, CancellationToken ct) =>
            {
                var c =
                    await db
                        .Customers.AsNoTracking()
                        .SingleOrDefaultAsync(c => c.PublicId == id, ct)
                    ?? throw ApiError.NotFound();
                return new
                {
                    id = c.PublicId,
                    c.Email,
                    c.FirstName,
                    c.LastName,
                    c.Status,
                    c.CreatedAt,
                    addresses = (
                        await db
                            .CustomerAddresses.AsNoTracking()
                            .Where(a => a.CustomerId == c.Id)
                            .ToListAsync(ct)
                    ).Select(ToDto),
                    recentOrders = await db
                        .Orders.AsNoTracking()
                        .Where(o => o.CustomerId == c.Id)
                        .OrderByDescending(o => o.CreatedAt)
                        .ThenByDescending(o => o.Id)
                        .Take(20)
                        .Select(o => new
                        {
                            id = o.PublicId,
                            o.OrderNumber,
                            o.Status,
                            o.GrandTotal,
                            o.Currency,
                            o.PlacedAt,
                        })
                        .ToListAsync(ct),
                };
            }
        );
        admin
            .MapPatch(
                "/{id:guid}/status",
                async (Guid id, CustomerStatusRequest r, StoreDbContext db, CancellationToken ct) =>
                {
                    Rules.Require(r.Status is "active" or "disabled", "只支持启用或停用客户。");
                    var c =
                        await db.Customers.SingleOrDefaultAsync(c => c.PublicId == id, ct)
                        ?? throw ApiError.NotFound();
                    c.Status = r.Status;
                    c.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    return Results.NoContent();
                }
            )
            .RequireAuthorization("AdminWrite");
    }

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
