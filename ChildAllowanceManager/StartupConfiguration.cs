namespace ChildAllowanceManager;

public static class StartupConfiguration
{
    public static bool UseAzureMonitor(IConfiguration configuration) =>
        StartupPolicy.IsConfigured(configuration["AzureMonitor:ConnectionString"]);

    public static bool ShouldMigrate(IHostEnvironment environment, IEnumerable<string> args) =>
        environment.IsDevelopment() || args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);
}
