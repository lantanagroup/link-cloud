using Automation.UI.Services.Persistence;
using FluentAssertions;
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
    public async Task Observed_header_filter_misses_after_an_intervening_small_write()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        await store.SetDomainAsync(runId, "entries", "first", CancellationToken.None);

        var collection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var original = await collection.Find(doc => doc.RunId == runId && doc.Domain == "entries").SingleAsync();
        var observed = new DomainSnapshotDocument
        {
            Id = original.Id,
            UpdatedAt = original.UpdatedAt,
            ChunkIndex = original.ChunkIndex,
            Revision = original.Revision
        };
        (await collection.Find(MongoSnapshotStore.ObservedHeaderFilter(observed)).CountDocumentsAsync())
            .Should().Be(1);

        await collection.UpdateOneAsync(
            doc => doc.Id == original.Id,
            Builders<DomainSnapshotDocument>.Update
                .Set(doc => doc.Data, "\"second\"")
                .Set(doc => doc.UpdatedAt, original.UpdatedAt.AddMinutes(1)));

        var matched = await collection.UpdateOneAsync(
            MongoSnapshotStore.ObservedHeaderFilter(observed),
            Builders<DomainSnapshotDocument>.Update.Set(doc => doc.Data, "\"stale\""));
        matched.MatchedCount.Should().Be(0);
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be("second");
    }

    [Fact]
    public async Task SetDomainAsync_small_write_keeps_slices_from_another_revision()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var chunked = new string('a', MongoSnapshotStore.SnapshotChunkBytes - 1);
        await store.SetDomainAsync(runId, "entries", chunked, CancellationToken.None);

        var collection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        await collection.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "entries",
            Data = "foreign-slice",
            ChunkIndex = 0,
            ChunkCount = 1,
            Revision = "foreignrevisionforeignrevision00",
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        });

        await store.SetDomainAsync(runId, "entries", "kept", CancellationToken.None);

        var docs = await Docs(runId);
        docs.Should().Contain(d => d.Revision == "foreignrevisionforeignrevision00" && d.Data == "foreign-slice");
        docs.Should().NotContain(d => d.ChunkIndex >= 0 && d.Data.Contains('a'));
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be("kept");
    }

    [Fact]
    public async Task SetDomainAsync_drops_an_older_header_and_its_slices()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        await store.SetDomainAsync(runId, "entries", "current", CancellationToken.None);

        var collection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        await collection.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "entries",
            Data = string.Empty,
            ChunkIndex = -1,
            ChunkCount = 1,
            Revision = "oldrevisionoldrevisionoldrevis",
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        await collection.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "entries",
            Data = "old-slice",
            ChunkIndex = 0,
            ChunkCount = 1,
            Revision = "oldrevisionoldrevisionoldrevis",
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        });

        await store.SetDomainAsync(runId, "entries", "kept", CancellationToken.None);

        var docs = await Docs(runId);
        docs.Should().NotContain(doc => doc.Revision == "oldrevisionoldrevisionoldrevis");
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be("kept");
    }

    [Fact]
    public async Task SetDomainAsync_does_not_replace_a_header_newer_than_this_write()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        await store.SetDomainAsync(runId, "entries", "current", CancellationToken.None);

        var collection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var header = await collection.Find(doc => doc.RunId == runId && doc.Domain == "entries").SingleAsync();
        await collection.UpdateOneAsync(
            doc => doc.Id == header.Id,
            Builders<DomainSnapshotDocument>.Update
                .Set(doc => doc.Data, "\"newer\"")
                .Set(doc => doc.UpdatedAt, DateTimeOffset.UtcNow.AddMinutes(5)));
        await SetClock(runId, "entries", DateTimeOffset.UtcNow.AddMinutes(5));

        await store.SetDomainAsync(runId, "entries", "stale", CancellationToken.None);

        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be("newer");
    }

    [Fact]
    public async Task SetDomainAsync_does_not_slice_over_a_header_newer_than_this_write()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var current = new string('a', MongoSnapshotStore.SnapshotChunkBytes - 1);
        await store.SetDomainAsync(runId, "entries", current, CancellationToken.None);

        var collection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var header = await collection.Find(doc => doc.RunId == runId && doc.Domain == "entries" && doc.ChunkIndex == -1).SingleAsync();
        await collection.UpdateOneAsync(
            doc => doc.Id == header.Id,
            Builders<DomainSnapshotDocument>.Update
                .Set(doc => doc.UpdatedAt, DateTimeOffset.UtcNow.AddMinutes(5)));
        await SetClock(runId, "entries", DateTimeOffset.UtcNow.AddMinutes(5));

        var stale = new string('b', MongoSnapshotStore.SnapshotChunkBytes - 1);
        await store.SetDomainAsync(runId, "entries", stale, CancellationToken.None);

        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be(current);
        (await Docs(runId)).Should().NotContain(doc => doc.Data.Contains('b'));
    }

    [Fact]
    public async Task SetDomainAsync_concurrent_chunked_writes_keep_one_revision()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var first = new string('a', MongoSnapshotStore.SnapshotChunkBytes - 1);
        var second = new string('b', MongoSnapshotStore.SnapshotChunkBytes - 1);

        await Task.WhenAll(
            store.SetDomainAsync(runId, "entries", first, CancellationToken.None),
            store.SetDomainAsync(runId, "entries", second, CancellationToken.None));

        var read = await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None);
        read.Should().NotBeNull();
        read!.Data.Should().BeOneOf(first, second);

        var docs = await Docs(runId);
        var headers = docs.Where(doc => doc.ChunkIndex is null or -1).ToList();
        headers.Should().ContainSingle();
        var winnerRevision = headers[0].Revision;
        docs.Where(doc => doc.ChunkIndex >= 0).Should().OnlyContain(doc => doc.Revision == winnerRevision);
    }

    [Fact]
    public async Task SetDomainAsync_concurrent_writes_leave_a_readable_snapshot()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();

        await Task.WhenAll(
            store.SetDomainAsync(runId, "entries", "one", CancellationToken.None),
            store.SetDomainAsync(runId, "entries", "two", CancellationToken.None));

        var read = await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None);
        read.Should().NotBeNull();
        read!.Data.Should().BeOneOf("one", "two");
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
    public async Task SetDomainAsync_reclaims_slices_when_the_single_document_update_is_not_acknowledged()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var chunked = new string('a', MongoSnapshotStore.SnapshotChunkBytes - 1);
        await store.SetDomainAsync(runId, "entries", chunked, CancellationToken.None);

        store.AfterSingleHeaderUpdate = () => throw new IOException("ack lost");

        var act = () => store.SetDomainAsync(runId, "entries", "kept", CancellationToken.None);
        await act.Should().ThrowAsync<IOException>();

        var docs = await Docs(runId);
        docs.Should().NotContain(doc => doc.ChunkIndex >= 0);
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be("kept");
    }

    [Fact]
    public async Task SetDomainAsync_drops_a_displaced_revision_when_a_newer_header_wins()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var chunked = new string('a', MongoSnapshotStore.SnapshotChunkBytes - 1);
        await store.SetDomainAsync(runId, "entries", chunked, CancellationToken.None);

        var collection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        store.BeforeDeleteDisplaced = () => collection.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "entries",
            Data = "\"winner\"",
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(5)
        });

        await store.SetDomainAsync(runId, "entries", "loser", CancellationToken.None);

        var docs = await Docs(runId);
        docs.Should().NotContain(doc => doc.ChunkIndex >= 0);
        docs.Should().ContainSingle(doc => doc.Data == "\"winner\"");
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be("winner");
    }

    [Fact]
    public async Task SetDomainAsync_reclaims_a_displaced_revision_when_its_header_is_gone()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var chunked = new string('a', MongoSnapshotStore.SnapshotChunkBytes - 1);
        await store.SetDomainAsync(runId, "entries", chunked, CancellationToken.None);

        var collection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        store.BeforeDeleteDisplaced = () => collection.DeleteManyAsync(
            doc => doc.RunId == runId && doc.Domain == "entries" && (doc.ChunkIndex == null || doc.ChunkIndex == -1));

        await store.SetDomainAsync(runId, "entries", "loser", CancellationToken.None);

        var docs = await Docs(runId);
        docs.Should().NotContain(doc => doc.ChunkIndex >= 0);
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task SetDomainAsync_reclaims_the_previous_revision_when_a_chunked_header_update_is_not_acknowledged()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var chunked = new string('a', MongoSnapshotStore.SnapshotChunkBytes - 1);
        await store.SetDomainAsync(runId, "entries", chunked, CancellationToken.None);

        var collection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var original = (await Docs(runId)).Single(doc => doc.ChunkIndex == -1).Revision;
        const string replacement = "r2r2r2r2r2r2r2r2r2r2r2r2r2r2r2r2";
        store.AfterChunkedHeaderUpdate = async () =>
        {
            var header = await collection.Find(doc => doc.RunId == runId && doc.Domain == "entries" && doc.ChunkIndex == -1).SingleAsync();
            await collection.UpdateOneAsync(
                doc => doc.Id == header.Id,
                Builders<DomainSnapshotDocument>.Update
                    .Set(doc => doc.Revision, replacement)
                    .Set(doc => doc.ChunkCount, 1)
                    .Set(doc => doc.Data, string.Empty));
            await collection.InsertOneAsync(new DomainSnapshotDocument
            {
                RunId = runId,
                Domain = "entries",
                Data = "\"winner\"",
                ChunkIndex = 0,
                ChunkCount = 1,
                Revision = replacement,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            throw new IOException("ack lost");
        };

        var act = () => store.SetDomainAsync(runId, "entries", new string('b', MongoSnapshotStore.SnapshotChunkBytes - 1), CancellationToken.None);
        await act.Should().ThrowAsync<IOException>();

        var docs = await Docs(runId);
        docs.Should().NotContain(doc => doc.Revision == original);
        docs.Where(doc => doc.ChunkIndex >= 0).Should().OnlyContain(doc => doc.Revision == replacement);
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be("winner");
    }

    [Fact]
    public async Task SetDomainAsync_advances_the_timestamp_when_the_clock_does_not()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var frozen = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        store.Clock = () => frozen;
        var runId = Guid.NewGuid();
        var collection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");

        await store.SetDomainAsync(runId, "schedule", "one", CancellationToken.None);
        var first = await collection.Find(doc => doc.RunId == runId && doc.Domain == "schedule").SingleAsync();
        first.UpdatedAt.Should().Be(frozen);

        await store.SetDomainAsync(runId, "schedule", "two", CancellationToken.None);
        var second = await collection.Find(doc => doc.RunId == runId && doc.Domain == "schedule").SingleAsync();
        second.UpdatedAt.Should().Be(frozen.AddMilliseconds(1));
        (await Clock(runId, "schedule")).WriteClock.Should().Be(frozen);
        second.Data.Should().Be("\"two\"");

        await store.SetDomainAsync(runId, "schedule", "three", CancellationToken.None);
        var third = await collection.Find(doc => doc.RunId == runId && doc.Domain == "schedule").SingleAsync();
        third.UpdatedAt.Should().Be(frozen.AddMilliseconds(2));
        (await Clock(runId, "schedule")).WriteClock.Should().Be(frozen);
        third.Data.Should().Be("\"three\"");

        var matched = await collection.UpdateOneAsync(
            MongoSnapshotStore.ObservedHeaderFilter(first),
            Builders<DomainSnapshotDocument>.Update.Set(doc => doc.Data, "\"stale\""));
        matched.MatchedCount.Should().Be(0);
        (await store.GetDomainAsync<string>(runId, "schedule", CancellationToken.None))!.Data.Should().Be("three");
    }

    [Fact]
    public async Task SetDomainAsync_keeps_three_chunked_writes_when_the_clock_does_not_move()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var frozen = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        store.Clock = () => frozen;
        var runId = Guid.NewGuid();

        await store.SetDomainAsync(runId, "entries", new string('a', MongoSnapshotStore.SnapshotChunkBytes + 1), CancellationToken.None);
        await store.SetDomainAsync(runId, "entries", new string('b', MongoSnapshotStore.SnapshotChunkBytes + 1), CancellationToken.None);
        await store.SetDomainAsync(runId, "entries", new string('c', MongoSnapshotStore.SnapshotChunkBytes + 1), CancellationToken.None);

        var header = (await Docs(runId)).Single(doc => doc.ChunkIndex == -1);
        (await Clock(runId, "entries")).WriteClock.Should().Be(frozen);
        header.UpdatedAt.Should().Be(frozen.AddMilliseconds(2));
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data
            .Should().Be(new string('c', MongoSnapshotStore.SnapshotChunkBytes + 1));
    }

    [Fact]
    public async Task SetDomainAsync_drops_a_write_whose_clock_is_behind_the_stored_clock()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var later = new DateTimeOffset(2026, 4, 5, 6, 7, 8, TimeSpan.Zero);
        store.Clock = () => later;
        await store.SetDomainAsync(runId, "schedule", "newer", CancellationToken.None);

        store.Clock = () => later.AddMinutes(-10);
        await store.SetDomainAsync(runId, "schedule", "older", CancellationToken.None);

        (await store.GetDomainAsync<string>(runId, "schedule", CancellationToken.None))!.Data.Should().Be("newer");
    }

    [Fact]
    public async Task SetDomainAsync_reclaims_the_previous_revision_when_the_publish_followup_lookup_fails()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        await store.SetDomainAsync(runId, "entries", new string('a', MongoSnapshotStore.SnapshotChunkBytes + 1), CancellationToken.None);
        var original = (await Docs(runId)).Single(doc => doc.ChunkIndex == -1).Revision;

        store.AfterChunkedHeaderUpdate = () => throw new IOException("ack lost");
        store.AfterPublishConfirmed = () =>
        {
            store.FailNextHeaderLookup = true;
            return Task.CompletedTask;
        };

        var replacement = new string('b', MongoSnapshotStore.SnapshotChunkBytes + 1);
        var act = () => store.SetDomainAsync(runId, "entries", replacement, CancellationToken.None);
        await act.Should().ThrowAsync<IOException>();

        var docs = await Docs(runId);
        docs.Should().NotContain(doc => doc.Revision == original);
        docs.Should().Contain(doc => doc.ChunkIndex == -1 && doc.Revision != original);
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be(replacement);
    }

    [Fact]
    public async Task SetDomainAsync_reclaims_the_previous_revision_when_the_displaced_lookup_fails()
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        var runId = Guid.NewGuid();
        var chunked = new string('a', MongoSnapshotStore.SnapshotChunkBytes - 1);
        await store.SetDomainAsync(runId, "entries", chunked, CancellationToken.None);
        var original = (await Docs(runId)).Single(doc => doc.ChunkIndex == -1).Revision;

        store.BeforeDeleteDisplaced = () =>
        {
            store.FailNextHeaderLookup = true;
            return Task.CompletedTask;
        };

        var replacement = new string('b', MongoSnapshotStore.SnapshotChunkBytes - 1);
        var act = () => store.SetDomainAsync(runId, "entries", replacement, CancellationToken.None);
        await act.Should().ThrowAsync<IOException>();

        var docs = await Docs(runId);
        docs.Should().NotContain(doc => doc.Revision == original);
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be(replacement);
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
    public async Task SetDomainAsync_a_paused_writer_does_not_roll_the_clock_back()
    {
        var origin = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var runId = Guid.NewGuid();
        var seed = StoreAt(origin);
        var paused = StoreAt(origin.AddMinutes(10));
        var newer = StoreAt(origin.AddMinutes(30));
        await seed.SetDomainAsync(runId, "schedule", "seed", CancellationToken.None);

        paused.AfterClockClaimed = () =>
        {
            paused.AfterClockClaimed = null;
            return newer.SetDomainAsync(runId, "schedule", "from-newer", CancellationToken.None);
        };

        await paused.SetDomainAsync(runId, "schedule", "from-paused", CancellationToken.None);

        (await paused.GetDomainAsync<string>(runId, "schedule", CancellationToken.None))!.Data.Should().Be("from-newer");
        (await Clock(runId, "schedule")).WriteClock.Should().Be(origin.AddMinutes(30));
    }

    [Fact]
    public async Task SetDomainAsync_a_paused_chunked_writer_does_not_roll_the_clock_back()
    {
        var origin = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var runId = Guid.NewGuid();
        var seed = StoreAt(origin);
        var paused = StoreAt(origin.AddMinutes(10));
        var newer = StoreAt(origin.AddMinutes(30));
        var newerPayload = new string('b', MongoSnapshotStore.SnapshotChunkBytes + 1);
        await seed.SetDomainAsync(runId, "entries", "seed", CancellationToken.None);

        paused.AfterClockClaimed = () =>
        {
            paused.AfterClockClaimed = null;
            return newer.SetDomainAsync(runId, "entries", newerPayload, CancellationToken.None);
        };

        await paused.SetDomainAsync(runId, "entries", new string('a', MongoSnapshotStore.SnapshotChunkBytes + 1), CancellationToken.None);

        (await paused.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be(newerPayload);
        (await Clock(runId, "entries")).WriteClock.Should().Be(origin.AddMinutes(30));
    }

    [Fact]
    public async Task SetDomainAsync_a_claimed_clock_rejects_an_older_writer()
    {
        var origin = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var runId = Guid.NewGuid();
        var seed = StoreAt(origin);
        var paused = StoreAt(origin.AddMinutes(30));
        var older = StoreAt(origin.AddMinutes(20));
        await seed.SetDomainAsync(runId, "schedule", "seed", CancellationToken.None);

        paused.AfterClockClaimed = async () =>
        {
            paused.AfterClockClaimed = null;
            await older.SetDomainAsync(runId, "schedule", "from-older", CancellationToken.None);
            (await paused.GetDomainAsync<string>(runId, "schedule", CancellationToken.None))!.Data.Should().Be("seed");
        };

        await paused.SetDomainAsync(runId, "schedule", "from-paused", CancellationToken.None);

        (await paused.GetDomainAsync<string>(runId, "schedule", CancellationToken.None))!.Data.Should().Be("from-paused");
        (await Clock(runId, "schedule")).WriteClock.Should().Be(origin.AddMinutes(30));
    }

    [Fact]
    public async Task SetDomainAsync_a_claimed_chunked_clock_rejects_an_older_writer()
    {
        var origin = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var runId = Guid.NewGuid();
        var seed = StoreAt(origin);
        var paused = StoreAt(origin.AddMinutes(30));
        var older = StoreAt(origin.AddMinutes(20));
        var pausedPayload = new string('a', MongoSnapshotStore.SnapshotChunkBytes + 1);
        await seed.SetDomainAsync(runId, "entries", "seed", CancellationToken.None);

        paused.AfterClockClaimed = async () =>
        {
            paused.AfterClockClaimed = null;
            await older.SetDomainAsync(runId, "entries", new string('b', MongoSnapshotStore.SnapshotChunkBytes + 1), CancellationToken.None);
            (await paused.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be("seed");
        };

        await paused.SetDomainAsync(runId, "entries", pausedPayload, CancellationToken.None);

        (await paused.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be(pausedPayload);
        (await Clock(runId, "entries")).WriteClock.Should().Be(origin.AddMinutes(30));
    }

    private MongoSnapshotStore StoreAt(DateTimeOffset now)
    {
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance);
        store.Clock = () => now;
        return store;
    }

    private Task<List<DomainSnapshotDocument>> Docs(Guid runId)
        => _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots")
            .Find(doc => doc.RunId == runId && doc.Domain == "entries")
            .ToListAsync();

    private Task SetClock(Guid runId, string domain, DateTimeOffset clock)
    {
        var id = MongoSnapshotStore.SnapshotClockId(runId, domain);
        return _fixture.Database.GetCollection<SnapshotWriteClockDocument>("automation_snapshot_clocks")
            .ReplaceOneAsync(
                c => c.Id == id,
                new SnapshotWriteClockDocument
                {
                    Id = id,
                    RunId = runId,
                    Domain = domain,
                    WriteClock = clock
                },
                new ReplaceOptions { IsUpsert = true });
    }

    private Task<SnapshotWriteClockDocument> Clock(Guid runId, string domain)
    {
        var id = MongoSnapshotStore.SnapshotClockId(runId, domain);
        return _fixture.Database.GetCollection<SnapshotWriteClockDocument>("automation_snapshot_clocks")
            .Find(c => c.Id == id)
            .SingleAsync();
    }

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
