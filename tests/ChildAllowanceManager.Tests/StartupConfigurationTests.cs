using ChildAllowanceManager;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ChildAllowanceManager.Tests;

public class StartupConfigurationTests
{
    [Fact]
    public void RepositoryDefaultsAreSafeAndTelemetryIsOffInDevelopment()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
        var environment = new TestHostEnvironment(Environments.Development);

        Assert.False(StartupConfiguration.UseAzureMonitor(environment, configuration));
        Assert.True(StartupConfiguration.UseAzureMonitor(
            new TestHostEnvironment(Environments.Production), configuration));
        Assert.Equal("localhost;127.0.0.1", configuration["AllowedHosts"]);
        Assert.Equal("'self'", new ServerComponentsEndpointOptions().ContentSecurityFrameAncestorsPolicy);
    }

    [Fact]
    public void AzureMonitorCanBeOptedIntoOutsideProduction()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureMonitor:Enabled"] = "true"
            })
            .Build();
        var environment = new TestHostEnvironment(Environments.Development);

        Assert.True(StartupConfiguration.UseAzureMonitor(environment, configuration));
    }

    [Fact]
    public void ProductionMigrationsRequireExplicitFlag()
    {
        var environment = new TestHostEnvironment(Environments.Production);

        Assert.False(StartupConfiguration.ShouldMigrate(environment, []));
        Assert.True(StartupConfiguration.ShouldMigrate(environment, ["--migrate"]));
        Assert.True(StartupConfiguration.ShouldMigrate(environment, ["--MIGRATE"]));
        Assert.True(StartupConfiguration.ShouldMigrate(
            new TestHostEnvironment(Environments.Development), []));
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = nameof(StartupConfigurationTests);
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
