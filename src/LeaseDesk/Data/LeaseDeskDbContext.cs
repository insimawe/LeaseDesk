using Microsoft.EntityFrameworkCore;

namespace LeaseDesk.Data;

public class LeaseDeskDbContext(DbContextOptions<LeaseDeskDbContext> options) : DbContext(options)
{
    public DbSet<LeaseContract> Contracts => Set<LeaseContract>();
}
