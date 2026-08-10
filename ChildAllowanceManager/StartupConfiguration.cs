namespace ChildAllowanceManager;

public static class StartupConfiguration
{
    public static bool UseAzureMonitor(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsProduction() || configuration.GetValue<bool>("AzureMonitor:Enabled");

    public static bool ShouldMigrate(IHostEnvironment environment, IEnumerable<string> args) =>
        environment.IsDevelopment() || args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);
}
