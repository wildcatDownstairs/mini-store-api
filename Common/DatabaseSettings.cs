using Npgsql;

namespace MiniStore.Common;

public static class DatabaseSettings
{
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
