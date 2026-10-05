using Automation.UI.Services.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
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
    public async Task SetDomainAsync_stores_a_payload_at_the_chunk_limit_as_one_document()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var payload = new string('a', MongoSnapshotStore.SnapshotChunkBytes - 2);

        await store.SetDomainAsync(runId, "entries", payload, CancellationToken.None);

        var docs = await Docs(runId);
        docs.Should().ContainSingle();
        docs[0].ChunkIndex.Should().BeNull();
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be(payload);
    }

    [Fact]
    public async Task SetDomainAsync_round_trips_a_payload_one_byte_over_the_chunk_limit()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var payload = new string('a', MongoSnapshotStore.SnapshotChunkBytes - 1);

        await store.SetDomainAsync(runId, "entries", payload, CancellationToken.None);

        var docs = await Docs(runId);
        docs.Should().Contain(d => d.ChunkIndex == -1);
        docs.Count(d => d.ChunkIndex >= 0).Should().BeGreaterThan(1);
        docs.Where(d => d.ChunkIndex >= 0).Should().OnlyContain(d =>
            System.Text.Encoding.UTF8.GetByteCount(d.Data) <= MongoSnapshotStore.SnapshotChunkBytes);
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be(payload);
    }

    [Fact]
    public async Task SetDomainAsync_round_trips_a_multibyte_character_on_the_chunk_boundary()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var payload = new string('a', MongoSnapshotStore.SnapshotChunkBytes - 2) + "😀";

        await store.SetDomainAsync(runId, "entries", payload, CancellationToken.None);

        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be(payload);
    }

    [Fact]
    public async Task SetDomainAsync_replaces_a_chunked_snapshot_with_the_new_revision()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var first = new string('a', MongoSnapshotStore.SnapshotChunkBytes - 1);
        var second = new string('b', MongoSnapshotStore.SnapshotChunkBytes - 1);

        await store.SetDomainAsync(runId, "entries", first, CancellationToken.None);
        await store.SetDomainAsync(runId, "entries", second, CancellationToken.None);

        var docs = await Docs(runId);
        var header = docs.Single(d => d.ChunkIndex == -1);
        docs.Where(d => d.ChunkIndex >= 0).Should().OnlyContain(d => d.Revision == header.Revision);
        docs.Should().NotContain(d => d.Data.Contains('a'));
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be(second);
    }

    [Fact]
    public async Task GetDomainAsync_reads_a_legacy_single_document()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var collection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        await collection.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "entries",
            Data = "\"hello\"",
            UpdatedAt = DateTimeOffset.UtcNow
        });

        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be("hello");
    }

    [Fact]
    public async Task AppendLogsAsync_starts_a_new_chunk_before_1_5_MB()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var line = new string('x', 800_000);

        await store.AppendLogsAsync(runId, [line, line], CancellationToken.None);

        var chunks = await _fixture.Database.GetCollection<RunLogDocument>("automation_logs")
            .Find(log => log.RunId == runId)
            .ToListAsync();
        chunks.Should().HaveCount(2);
        chunks.Should().OnlyContain(chunk =>
            chunk.BsonByteCount <= MongoSnapshotStore.SnapshotChunkBytes
            && chunk.LineCount <= 1_000);
    }

    private Task<List<DomainSnapshotDocument>> Docs(Guid runId)
        => _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots")
            .Find(doc => doc.RunId == runId && doc.Domain == "entries")
            .ToListAsync();
}
