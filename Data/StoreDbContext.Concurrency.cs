using Microsoft.EntityFrameworkCore;
using MiniStore.Data.Entities;

namespace MiniStore.Data;

public partial class StoreDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // PostgreSQL 每次更新行都会改变 xmin；EF 把它放进 UPDATE 的 WHERE 中，防止覆盖他人的编辑。
        modelBuilder
            .Entity<Product>()
            .Property<uint>("Version")
            .IsRowVersion()
            .HasColumnName("xmin");
    }
}
