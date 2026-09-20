using Npgsql;

namespace MiniStore.Common;

/// <summary>解析数据库连接配置；不会初始化数据库，也不会把连接密码输出到日志。</summary>
public static class DatabaseSettings
{
    /// <summary>完整 EcommerceLab 连接串优先；未配置时读取 PG 环境变量及本机默认值。</summary>
    public static string Connection(IConfiguration config)
    {
        if (config.GetConnectionString("EcommerceLab") is { Length: > 0 } configured)
            return configured;
        // 连接字符串构造器负责转义；不能手工拼接密码。
        return new NpgsqlConnectionStringBuilder
        {
            Host = Environment.GetEnvironmentVariable("PGHOST") ?? "/tmp",
            Port = int.Parse(Environment.GetEnvironmentVariable("PGPORT") ?? "5432"),
            Database = Environment.GetEnvironmentVariable("PGDATABASE") ?? "ecommerce_lab",
            Username = Environment.GetEnvironmentVariable("PGUSER") ?? Environment.UserName,
            Password = Environment.GetEnvironmentVariable("PGPASSWORD") ?? "",
            ApplicationName = "MiniStore.Api",
            Timeout = 10,
            CommandTimeout = 30,
            IncludeErrorDetail = false,
        }.ConnectionString;
    }
}
