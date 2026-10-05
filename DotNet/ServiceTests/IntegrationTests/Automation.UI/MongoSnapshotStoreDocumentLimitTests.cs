using Automation.UI.Services.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.AutomationUI;

[Collection(IntegrationTestCollection.Name)]
public class MongoSnapshotStoreDocumentLimitTests : IAsyncLifetime
{
    private readonly AutomationUIIntegrationTestFixture _fixture;

    public MongoSnapshotStoreDocumentLimitTests(AutomationUIIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SetDomainAsync_skips_a_document_over_the_cosmos_limit_and_keeps_a_smaller_one()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var oversized = new string('a', MongoSnapshotStore.CosmosDocumentLimitBytes);

        await store.SetDomainAsync(runId, "entries", oversized, CancellationToken.None);

        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None)).Should().BeNull();

        await store.SetDomainAsync(runId, "entries", "kept", CancellationToken.None);
        var stored = await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None);
        stored!.Data.Should().Be("kept");
    }
}
