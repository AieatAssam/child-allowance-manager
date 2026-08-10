namespace ChildAllowanceManager;

public static class StartupConfiguration
{
    public static bool UseAzureMonitor(IHostEnvironment environment, IConfiguration configuration)
    {
        var connectionString = configuration["AzureMonitor:ConnectionString"];
        if (StartupPolicy.IsConfigured(connectionString))
            return true;

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "AzureMonitor:ConnectionString is required outside Development. " +
                "Set the AzureMonitor__ConnectionString environment variable to the " +
                "Application Insights connection string, or run with " +
                "ASPNETCORE_ENVIRONMENT=Development for local work.");
        }

        return false;
    }

    public static bool ShouldMigrate(IHostEnvironment environment, IEnumerable<string> args) =>
        environment.IsDevelopment() || args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);
}
