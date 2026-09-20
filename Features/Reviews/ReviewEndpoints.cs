using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;
using MiniStore.Data.Entities;

namespace MiniStore.Features.Reviews;

public sealed record ReviewRequest(short Rating, string? Title, string? Content);

public sealed record ReviewDecision(string Status);

public static class ReviewEndpoints
{
    public static void MapReviews(this WebApplication app)
    {
        app.MapGet(
                "/api/store/products/{id:guid}/reviews",
                async (
                    Guid id,
                    StoreDbContext db,
                    [AsParameters] ListQuery q,
                    CancellationToken ct
                ) =>
                {
                    q.Validate();
                    return await db
                        .ProductReviews.AsNoTracking()
                        .Where(r =>
                            r.Product.PublicId == id
                            && r.Product.Status == "active"
                            && r.Product.DeletedAt == null
                            && r.Status == "published"
                        )
                        .OrderByDescending(r => r.CreatedAt)
                        .ThenByDescending(r => r.Id)
                        .Select(r => new
                        {
                            id = r.PublicId,
                            author = r.Customer.LastName + "顾客",
                            r.Rating,
                            r.Title,
                            r.Content,
                            r.IsVerifiedPurchase,
                            r.CreatedAt,
                        })
                        .PageAsync(q, ct);
                }
            )
            .WithTags("商品评价");
        app.MapPost(
                "/api/me/orders/{id:guid}/items/{itemId:guid}/review",
                async (
                    Guid id,
                    Guid itemId,
                    ReviewRequest r,
                    ClaimsPrincipal user,
                    StoreDbContext db,
                    CancellationToken ct
                ) =>
                {
                    Rules.Require(
                        r.Rating is >= 1 and <= 5
                            && r.Title?.Length is not > 160
                            && r.Content?.Length is not > 1000,
                        "评分须为 1～5，标题最多 160 字，评价最多 1000 字。"
                    );
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    var order = await RowLocks.Order(db, id, user.ActorId(), ct);
                    if (order.Status != "delivered")
                        throw ApiError.Conflict("签收后才能评价。");
                    var item =
                        await db.OrderItems.SingleOrDefaultAsync(
                            i => i.PublicId == itemId && i.OrderId == order.Id,
                            ct
                        ) ?? throw ApiError.NotFound();
                    var review = new ProductReview
                    {
                        CustomerId = order.CustomerId,
                        ProductId = item.ProductId,
                        OrderItemId = item.Id,
                        Rating = r.Rating,
                        Title = r.Title?.Trim(),
                        Content = r.Content?.Trim(),
                        IsVerifiedPurchase = true,
                        Status = "pending",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    };
                    db.ProductReviews.Add(review);
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return Results.Ok(new { id = review.PublicId, review.Status });
                }
            )
            .RequireAuthorization("Customer")
            .WithTags("商品评价");
        var admin = app.MapGroup("/api/admin/reviews")
            .RequireAuthorization("AdminRead")
            .WithTags("评价审核");
        admin.MapGet(
            "",
            async (StoreDbContext db, [AsParameters] ListQuery q, CancellationToken ct) =>
            {
                q.Validate();
                var source = db.ProductReviews.AsNoTracking();
                if (q.Status != null)
                    source = source.Where(r => r.Status == q.Status);
                if (q.Q is { Length: > 0 })
                {
                    var pattern = "%" + q.Q.Trim() + "%";
                    source = source.Where(r =>
                        EF.Functions.ILike(r.Product.Name, pattern)
                        || r.Content != null && EF.Functions.ILike(r.Content, pattern)
                    );
                }
                return await source
                    .OrderByDescending(r => r.CreatedAt)
                    .ThenByDescending(r => r.Id)
                    .Select(r => new
                    {
                        id = r.PublicId,
                        productId = r.Product.PublicId,
                        product = r.Product.Name,
                        customer = r.Customer.LastName + " " + r.Customer.FirstName,
                        orderId = r.OrderItem != null ? (Guid?)r.OrderItem.Order.PublicId : null,
                        orderNumber = r.OrderItem != null ? r.OrderItem.Order.OrderNumber : null,
                        r.Rating,
                        r.Title,
                        r.Content,
                        r.IsVerifiedPurchase,
                        r.Status,
                        r.CreatedAt,
                    })
                    .PageAsync(q, ct);
            }
        );
        admin
            .MapPost(
                "/{id:guid}/review",
                async (Guid id, ReviewDecision r, StoreDbContext db, CancellationToken ct) =>
                {
                    Rules.Require(r.Status is "published" or "rejected", "审核结果无效。");
                    var changed = await db
                        .ProductReviews.Where(x => x.PublicId == id && x.Status == "pending")
                        .ExecuteUpdateAsync(
                            s =>
                                s.SetProperty(x => x.Status, r.Status)
                                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow),
                            ct
                        );
                    if (changed == 0)
                        throw ApiError.Conflict("评价不存在或已审核。");
                    return Results.NoContent();
                }
            )
            .RequireAuthorization("AdminWrite");
    }
}
