using ChildAllowanceManager.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ChildAllowanceManager.HealthChecks;

public sealed class MigrationHealthCheck(AllowanceDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            return pending.Length == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy(
                    $"Database schema is out of date. Pending: {string.Join(", ", pending)}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Database is unreachable.", ex);
        }
    }
}
