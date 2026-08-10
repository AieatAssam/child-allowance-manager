using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChildAllowanceManager.Data;

public sealed class AllowanceDbContextFactory : IDesignTimeDbContextFactory<AllowanceDbContext>
{
    public AllowanceDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=55432;Database=child_allowance_manager_design;Username=postgres;Password=postgres";
        return new AllowanceDbContext(new DbContextOptionsBuilder<AllowanceDbContext>()
            .UseNpgsql(connection)
            .Options);
    }
}
