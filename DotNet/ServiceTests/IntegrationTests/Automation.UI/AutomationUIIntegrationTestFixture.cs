using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Configuration;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using Testcontainers.MongoDb;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.AutomationUI;

/// <summary>
/// Spins up a real MongoDB instance in a container so we can exercise the persistence
/// layer of <see cref="MongoSnapshotStore"/> and the startup migration in
/// <see cref="MongoIndexManager"/> end-to-end.
///
/// An in-memory fake would not surface the bugs this suite exists to protect against
/// � specifically the BSON canonical type-order rules that make a $gte filter against
/// an ISODate silently drop documents whose DateTimeOffset fields are still persisted
/// in the driver's default array-form representation.
/// </summary>
public sealed class AutomationUIIntegrationTestFixture : IAsyncLifetime, IDisposable
{
    // Pin an LTS image tag so CI caching is stable and pulls are deterministic.
    private readonly MongoDbContainer _mongo = new MongoDbBuilder()
        .WithImage("mongo:7.0")
        .Build();

    private MongoClient? _client;
    private bool _disposed;

    public IMongoDatabase Database { get; private set; } = null!;

    /// <summary>Collection handle typed to the production document so tests and the
    /// store share identical class-map configuration.</summary>
    public IMongoCollection<AutomationRunDocument> RunsCollection =>
        Database.GetCollection<AutomationRunDocument>("automation_runs");

    /// <summary>Raw BSON handle for seeding legacy-shaped documents that bypass the
    /// typed serializer.</summary>
    public IMongoCollection<MongoDB.Bson.BsonDocument> RawRunsCollection =>
        Database.GetCollection<MongoDB.Bson.BsonDocument>("automation_runs");

    public MongoSnapshotStore CreateStore() =>
        new(Database, NullLogger<MongoSnapshotStore>.Instance);

    public MongoIndexManager CreateIndexManager() =>
        new(Database, NullLogger<MongoIndexManager>.Instance);

    /// <summary>
    /// Builds a real <see cref="AutomationRunManager"/> wired to the container-backed
    /// store so we can exercise <see cref="AutomationRunManager.GetDashboardStatsAsync"/>
    /// end-to-end. Dependencies that the dashboard path doesn't touch
    /// (SignalR hub, query-plan template store, host services) are mocked; the
    /// orchestrator is constructed without being started.
    /// </summary>
    public AutomationRunManager CreateRunManager()
    {
        var store = CreateStore();
        var orchestrator = new RunSnapshotOrchestrator(
            store,
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<RunSnapshotOrchestrator>.Instance);

        return new AutomationRunManager(
            Mock.Of<IHubContext<RunHub>>(),
            Options.Create(new AutomationConfig()),
            NullLogger<AutomationRunManager>.Instance,
            new ServiceCollection().BuildServiceProvider(),
            new ConfigurationBuilder().Build(),
            orchestrator,
            store,
            Mock.Of<IQueryPlanTemplateStore>(),
            Mock.Of<INormalizationStore>(),
            Mock.Of<IOrganizationResourceMapTemplateStore>());
    }

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();
        _client = new MongoClient(_mongo.GetConnectionString());
        // Each fixture instance uses a unique DB to keep parallel collections (if any)
        // from colliding. xUnit serializes tests within the IntegrationTests collection,
        // but being defensive costs nothing here.
        Database = _client.GetDatabase($"automation_ui_tests_{Guid.NewGuid():N}");
    }

    /// <summary>Drops the automation_runs collection so each test starts from a known
    /// empty state without restarting the container.</summary>
    public Task ResetAsync()
        => Database.DropCollectionAsync("automation_runs");

    public async Task DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            await _mongo.DisposeAsync();
        }
        catch
        {
            // best effort on teardown
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        DisposeAsync().GetAwaiter().GetResult();
    }
}
