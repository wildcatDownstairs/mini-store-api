using Microsoft.EntityFrameworkCore;
using MiniStore.Features.Products;

namespace MiniStore.Data;

public partial class StoreDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // PostgreSQL 每次更新行都会改变 xmin；EF 把它放进 UPDATE 的 WHERE 中，防止覆盖他人的编辑。
        // Version 是影子属性：Product.cs 没有这个 C# 属性，查询用 EF.Property，更新用 Entry.Property。
        // IsRowVersion 同时声明数据库生成值和并发检查；过期版本由公共异常处理器转换为 HTTP 409。
        modelBuilder
            .Entity<Product>()
            .Property<uint>("Version")
            .IsRowVersion()
            .HasColumnName("xmin");
    }
}
