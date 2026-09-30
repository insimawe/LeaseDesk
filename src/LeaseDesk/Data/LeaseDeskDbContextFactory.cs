using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace LeaseDesk.Data;

public class LeaseDeskDbContextFactory : IDesignTimeDbContextFactory<LeaseDeskDbContext>
{
    public LeaseDeskDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<LeaseDeskDbContext>()
            .UseSqlite(SqlitePath.ResolveConnectionString(configuration))
            .Options;

        return new LeaseDeskDbContext(options);
    }
}
