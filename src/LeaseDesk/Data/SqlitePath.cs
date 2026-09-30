using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace LeaseDesk.Data;

public static class SqlitePath
{
    public const string DefaultConnectionString = "Data Source=leases.db";

    public static string ResolveConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("LeaseDesk") ?? DefaultConnectionString;

    public static void EnsureDirectory(string connectionString)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
            return;

        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }
}
