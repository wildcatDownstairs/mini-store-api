using MiniStore.Common;

namespace MiniStore.Features.Checkout;

/// <summary>纯计价函数不访问数据库，适合从 C# record、LINQ 和单元测试开始学习。</summary>
public sealed record PriceLine(
    Guid VariantId,
    string Sku,
    string ProductName,
    string VariantName,
    int Quantity,
    decimal UnitPrice,
    decimal TaxRate
);

public sealed record QuotedLine(
    Guid VariantId,
    string Sku,
    string ProductName,
    string VariantName,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal LineTotal
);

public sealed record Totals(
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal ShippingTotal,
    decimal GrandTotal,
    string Currency
);

public sealed record QuoteCore(IReadOnlyList<QuotedLine> Lines, Totals Totals);

public static class Pricing
{
    public static QuoteCore Calculate(
        IReadOnlyList<PriceLine> lines,
        decimal discount,
        string shipping
    )
    {
        Rules.Require(lines.Count is > 0 and <= 50, "购物车应有 1～50 种商品。");
        Rules.Require(shipping is "standard" or "express", "配送方式无效。");
        foreach (var line in lines)
        {
            Rules.Money(line.UnitPrice, "单价");
            Rules.Require(
                line.Quantity is > 0 and <= 99 && line.TaxRate is .08m or .10m,
                "数量或税率无效。"
            );
        }
        var subtotal = lines.Sum(l => l.Quantity * l.UnitPrice);
        Rules.Money(subtotal, "商品总额");
        Rules.Money(discount, "折扣");
        Rules.Require(discount <= subtotal, "折扣不能超过商品金额。");
        var cumulativeGross = 0m;
        var allocated = 0m;
        var result = new List<QuotedLine>();
        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            var gross = l.Quantity * l.UnitPrice;
            // 用累计比例分摊而不是把所有余数塞给末行；零元末行也不会出现负的折后金额。
            cumulativeGross += gross;
            var cumulative =
                subtotal == 0 ? 0 : decimal.Floor(discount * cumulativeGross / subtotal);
            var lineDiscount = cumulative - allocated;
            allocated = cumulative;
            var tax = decimal.Round(
                (gross - lineDiscount) * l.TaxRate,
                0,
                MidpointRounding.AwayFromZero
            );
            result.Add(
                new(
                    l.VariantId,
                    l.Sku,
                    l.ProductName,
                    l.VariantName,
                    l.Quantity,
                    l.UnitPrice,
                    lineDiscount,
                    tax,
                    gross - lineDiscount + tax
                )
            );
        }
        var freight =
            shipping == "express" ? 900m
            : subtotal - discount >= 8000 ? 0m
            : 500m;
        var total = subtotal - discount + result.Sum(l => l.TaxAmount) + freight;
        Rules.Money(total, "订单总额");
        return new(
            result,
            new(subtotal, discount, result.Sum(l => l.TaxAmount), freight, total, "JPY")
        );
    }
}
