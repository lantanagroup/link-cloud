using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
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
    public async Task GetDomainAsync_rejects_slices_that_do_not_start_at_zero()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var collection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        const string revision = "gaprevisiongaprevisiongaprevis";
        await collection.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "entries",
            Data = string.Empty,
            ChunkIndex = -1,
            ChunkCount = 1,
            Revision = revision,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await collection.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "entries",
            Data = "\"skipped\"",
            ChunkIndex = 1,
            ChunkCount = 1,
            Revision = revision,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None)).Should().BeNull();
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
    public async Task SetDomainAsync_omits_null_chunk_fields_on_a_single_document()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        await store.SetDomainAsync(runId, "entries", "plain", CancellationToken.None);

        var raw = await _fixture.Database.GetCollection<BsonDocument>("automation_snapshots")
            .Find(Builders<BsonDocument>.Filter.Eq("RunId", runId.ToString())
                & Builders<BsonDocument>.Filter.Eq("Domain", "entries"))
            .SingleAsync();
        raw.Contains("ChunkIndex").Should().BeFalse();
        raw.Contains("ChunkCount").Should().BeFalse();
        raw.Contains("Revision").Should().BeFalse();
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be("plain");
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
    [Fact]
    public async Task SetDomainAsync_single_document_stays_readable_by_the_old_snapshot_shape()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        await store.SetDomainAsync(runId, "schedule", "payload", CancellationToken.None);

        var raw = await _fixture.Database.GetCollection<BsonDocument>("automation_snapshots")
            .Find(Builders<BsonDocument>.Filter.Eq("RunId", runId.ToString()))
            .SingleAsync();
        raw.Names.Should().NotContain("WriteClock");
        raw.Names.Should().NotContain("ChunkIndex");
        raw.Names.Should().NotContain("ChunkCount");
        raw.Names.Should().NotContain("Revision");

        var legacy = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<LegacyDomainSnapshotDocument>(raw);
        legacy.Data.Should().Be("\"payload\"");
        legacy.Domain.Should().Be("schedule");
    }

    [Fact]
    public async Task SetDomainAsync_stores_an_empty_payload()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();

        await store.SetDomainAsync(runId, "schedule", "", CancellationToken.None);

        var docs = await _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots")
            .Find(doc => doc.RunId == runId)
            .ToListAsync();
        docs.Should().ContainSingle();
        docs[0].ChunkIndex.Should().BeNull();
        (await store.GetDomainAsync<string>(runId, "schedule", CancellationToken.None))!.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task SetDomainAsync_skips_a_second_write_when_the_payload_is_unchanged()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        await store.SetDomainAsync(runId, "schedule", "same", CancellationToken.None);
        var first = await _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots")
            .Find(doc => doc.RunId == runId && doc.Domain == "schedule")
            .SingleAsync();

        await Task.Delay(20);
        await store.SetDomainAsync(runId, "schedule", "same", CancellationToken.None);

        var docs = await _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots")
            .Find(doc => doc.RunId == runId && doc.Domain == "schedule")
            .ToListAsync();
        docs.Should().ContainSingle();
        docs[0].Id.Should().Be(first.Id);
        docs[0].UpdatedAt.Should().Be(first.UpdatedAt);
        (await store.GetDomainAsync<string>(runId, "schedule", CancellationToken.None))!.Data.Should().Be("same");
    }

    private Task<List<DomainSnapshotDocument>> Docs(Guid runId)
        => _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots")
            .Find(doc => doc.RunId == runId && doc.Domain == "entries")
            .ToListAsync();


    /// <summary>
    /// The snapshot fields a build from before the writer clock knew about.
    /// No <c>BsonIgnoreExtraElements</c>, matching that driver default.
    /// </summary>
    private sealed class LegacyDomainSnapshotDocument
    {
        public ObjectId Id { get; set; }

        [MongoDB.Bson.Serialization.Attributes.BsonRepresentation(BsonType.String)]
        public Guid RunId { get; set; }

        public string Domain { get; set; } = string.Empty;

        public string Data { get; set; } = string.Empty;

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
