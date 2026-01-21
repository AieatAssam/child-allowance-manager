using Microsoft.Azure.Cosmos;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.CosmosRepository.AspNetCore.Extensions;
using Microsoft.Azure.CosmosRepository.Options;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.CosmosDb;

namespace ChildAllowanceManager.CosmosDbTests.Fixtures;

public class CosmosDbFixture
{
    private const string DatabaseId = "child-allowance-manager-tests";
    private CosmosDbContainer _container = default!;
    private CosmosClient _client = default!;
    private IServiceProvider _serviceProvider = default!;

    public bool IsAvailable { get; private set; } = true;
    public string? SkipReason { get; private set; }

    public IRepository<TItem> GetRepository<TItem>() where TItem : Item
        => _serviceProvider.GetRequiredService<IRepository<TItem>>();

    public async Task InitializeAsync()
    {
        try
        {
            _container = new CosmosDbBuilder().Build();
            await _container.StartAsync();
        }
        catch (Exception ex) when (ex is Docker.DotNet.DockerApiException or DotNet.Testcontainers.Builders.DockerUnavailableException)
        {
            IsAvailable = false;
            SkipReason = $"Docker is unavailable for Cosmos DB tests: {ex.Message}";
            return;
        }

        _client = new CosmosClient(_container.GetConnectionString(), new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        });

        await _client.CreateDatabaseIfNotExistsAsync(DatabaseId);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_client);
        services.AddCosmosRepository(options =>
        {
            options.CosmosConnectionString = _container.GetConnectionString();
            options.DatabaseId = DatabaseId;
            options.ContainerPerItemType = true;
            options.AllowBulkExecution = true;
            options.SerializationOptions = new RepositorySerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            };
        });

        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task ResetAsync()
    {
        var database = _client.GetDatabase(DatabaseId);
        try
        {
            await database.DeleteAsync();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // database already removed
        }

        await _client.CreateDatabaseIfNotExistsAsync(DatabaseId);
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }

        _client?.Dispose();
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}
