using Microsoft.EntityFrameworkCore;
using MiniStore.Common;
using MiniStore.Data;

namespace MiniStore.Features.Reviews;

/// <summary>处理评价功能的数据查询与业务规则。同一次请求共用注入的 DbContext，事务边界保留在业务方法内。</summary>
public sealed class ReviewService(StoreDbContext db)
{
    /// <summary>公开读取仍上架商品的已发布评价；作者仅显示姓氏加称呼。</summary>
    public async Task<TableModel<ProductReviewDto>> ListProductAsync(
        Guid id,
        ListQuery q,
        CancellationToken ct
    )
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
            .Select(r => new ProductReviewDto(
                r.PublicId,
                r.Customer.LastName + "顾客",
                r.Rating,
                r.Title,
                r.Content,
                r.IsVerifiedPurchase,
                r.CreatedAt
            ))
            .PageAsync(q, ct);
    }

    /// <summary>锁定当前客户的已签收订单，核对订单项归属后创建待审核的已验证购买评价。</summary>
    public async Task<ReviewCreatedDto> CreateAsync(
        Guid id,
        Guid itemId,
        ReviewRequest r,
        long customerId,
        CancellationToken ct
    )
    {
        Rules.Require(
            r.Rating is >= 1 and <= 5
                && r.Title?.Length is not > 160
                && r.Content?.Length is not > 1000,
            "评分须为 1～5，标题最多 160 字，评价最多 1000 字。"
        );
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await RowLocks.Order(db, id, customerId, ct);
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
        return new ReviewCreatedDto(review.PublicId, review.Status);
    }

    /// <summary>后台分页检索评价，可查看待审核与拒绝状态及对应订单。</summary>
    public async Task<TableModel<ReviewSummaryDto>> ListAdminAsync(
        ListQuery q,
        CancellationToken ct
    )
    {
        q.Validate();
        var source = db.ProductReviews.AsNoTracking();
        if (q.Status != null)
            source = source.Where(r => r.Status == q.Status);
        if (q.Q is { Length: > 0 })
        {
            var pattern = Rules.ContainsPattern(q.Q);
            source = source.Where(r =>
                EF.Functions.ILike(r.Product.Name, pattern)
                || r.Content != null && EF.Functions.ILike(r.Content, pattern)
            );
        }
        return await source
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Select(r => new ReviewSummaryDto(
                r.PublicId,
                r.Product.PublicId,
                r.Product.Name,
                r.Customer.LastName + " " + r.Customer.FirstName,
                r.OrderItem != null ? (Guid?)r.OrderItem.Order.PublicId : null,
                r.OrderItem != null ? r.OrderItem.Order.OrderNumber : null,
                r.Rating,
                r.Title,
                r.Content,
                r.IsVerifiedPurchase,
                r.Status,
                r.CreatedAt
            ))
            .PageAsync(q, ct);
    }

    /// <summary>用条件 UPDATE 仅处理 pending 评价；检查受影响行数，防止重复审核覆盖先前结果。</summary>
    public async Task ModerateAsync(Guid id, ReviewDecision r, CancellationToken ct)
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
        // ExecuteUpdateAsync 立即发送 SQL，不经过实体跟踪器，不需要再调用 SaveChangesAsync。
        if (changed == 0)
            throw ApiError.Conflict("评价不存在或已审核。");
    }
}
