using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.AutomationUI;

/// <summary>
/// Local MongoDB does not enforce the Cosmos DB 2 MB document cap. These tests
/// put a guard in front of every collection write and reject a document whose
/// JSON exceeds that cap.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class MongoSnapshotStoreDocumentSizeTests : IAsyncLifetime
{
    private readonly AutomationUIIntegrationTestFixture _fixture;

    public MongoSnapshotStoreDocumentSizeTests(AutomationUIIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SetDomainAsync_partitions_records_over_one_megabyte_and_round_trips()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var payload = Enumerable.Range(0, 20)
            .Select(i => new Item(i.ToString(), new string('n', 60_000)))
            .ToList();

        await store.SetDomainAsync(runId, "acquisitionLogs", payload, CancellationToken.None);

        var read = await store.GetDomainAsync<List<Item>>(runId, "acquisitionLogs", CancellationToken.None);
        read.Should().NotBeNull();
        read!.Data.Should().Equal(payload);

        var parts = await Parts(runId, "acquisitionLogs");
        parts.Should().NotBeEmpty();
        parts.Select(p => p.GenerationId).Distinct().Should().HaveCount(1);
        parts.Should().OnlyContain(p => p.Kind == "element");
        await AssertRunDocumentsFit(runId);
    }

    [Fact]
    public async Task SetDomainAsync_splits_one_opaque_value_without_dropping_bytes()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var note = new string('z', 1_500_000);

        await store.SetDomainAsync(runId, "generationManifest", new Item("only", note), CancellationToken.None);

        var read = await store.GetDomainAsync<Item>(runId, "generationManifest", CancellationToken.None);
        read!.Data.Note.Should().Be(note);
        var parts = await Parts(runId, "generationManifest");
        parts.Should().Contain(p => p.Kind == "slice");
        await AssertRunDocumentsFit(runId);
    }

    [Fact]
    public async Task SetDomainAsync_rewrite_replaces_the_previous_generation()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var first = Enumerable.Range(0, 20).Select(i => new Item("a" + i, new string('a', 60_000))).ToList();
        var second = Enumerable.Range(0, 20).Select(i => new Item("b" + i, new string('b', 60_000))).ToList();

        await store.SetDomainAsync(runId, "entries", first, CancellationToken.None);
        await store.SetDomainAsync(runId, "entries", second, CancellationToken.None);

        var parts = await Parts(runId, "entries");
        parts.Select(p => p.GenerationId).Distinct().Should().HaveCount(1);
        var read = await store.GetDomainAsync<List<Item>>(runId, "entries", CancellationToken.None);
        read!.Data.Should().Equal(second);
        await AssertRunDocumentsFit(runId);
    }

    [Fact]
    public async Task GetDomainAsync_reads_a_legacy_inline_document()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var snapshots = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        await snapshots.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "schedule",
            Data = """{"Name":"legacy"}""",
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var read = await store.GetDomainAsync<LegacySchedule>(runId, "schedule", CancellationToken.None);
        read!.Data.Name.Should().Be("legacy");
    }

    [Fact]
    public async Task TryUpgradeLegacyDomainAsync_translates_once_and_leaves_a_small_document_alone()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var snapshots = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var large = JsonSerializerPayload(Enumerable.Range(0, 20).Select(i => new Item(i.ToString(), new string('q', 60_000))).ToList());
        var smallId = ObjectId.GenerateNewId();
        var largeId = ObjectId.GenerateNewId();
        await snapshots.InsertManyAsync(
        [
            new DomainSnapshotDocument
            {
                Id = smallId,
                RunId = runId,
                Domain = "schedule",
                Data = """{"Name":"stay"}""",
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new DomainSnapshotDocument
            {
                Id = largeId,
                RunId = runId,
                Domain = "populations",
                Data = large,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var small = await snapshots.Find(d => d.Id == smallId).FirstAsync();
        var oversized = await snapshots.Find(d => d.Id == largeId).FirstAsync();
        (await store.TryUpgradeLegacyDomainAsync(small, CancellationToken.None)).Should().BeFalse();
        (await store.TryUpgradeLegacyDomainAsync(oversized, CancellationToken.None)).Should().BeTrue();
        (await store.TryUpgradeLegacyDomainAsync(oversized, CancellationToken.None)).Should().BeFalse();

        var schedule = await store.GetDomainAsync<LegacySchedule>(runId, "schedule", CancellationToken.None);
        schedule!.Data.Name.Should().Be("stay");
        var populations = await store.GetDomainAsync<List<Item>>(runId, "populations", CancellationToken.None);
        populations!.Data.Should().HaveCount(20);
        (await snapshots.Find(d => d.Id == smallId).FirstAsync()).Data.Should().Be("""{"Name":"stay"}""");
        await AssertRunDocumentsFit(runId);
    }

    [Fact]
    public async Task Concurrent_rewrites_return_one_complete_generation()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var first = Enumerable.Range(0, 20).Select(i => new Item("a" + i, new string('a', 60_000))).ToList();
        var second = Enumerable.Range(0, 20).Select(i => new Item("b" + i, new string('b', 60_000))).ToList();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var left = WriteAfter(gate, store, runId, first);
        var right = WriteAfter(gate, store, runId, second);
        gate.SetResult();
        await Task.WhenAll(left, right);

        var read = await store.GetDomainAsync<List<Item>>(runId, "entries", CancellationToken.None);
        read.Should().NotBeNull();
        (read!.Data.SequenceEqual(first) || read.Data.SequenceEqual(second)).Should().BeTrue();
        (await Parts(runId, "entries")).Select(p => p.GenerationId).Distinct().Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteRunAsync_removes_snapshot_parts()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var payload = Enumerable.Range(0, 20).Select(i => new Item(i.ToString(), new string('n', 60_000))).ToList();
        await store.SetDomainAsync(runId, "acquisitionLogs", payload, CancellationToken.None);

        await store.DeleteRunAsync(runId, CancellationToken.None);

        (await Parts(runId, "acquisitionLogs")).Should().BeEmpty();
        (await store.GetDomainAsync<List<Item>>(runId, "acquisitionLogs", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task AppendLogs_and_split_keep_every_chunk_under_the_cap()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var lines = Enumerable.Range(0, 3).Select(i => new string((char)('a' + i), 400_000)).ToList();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        await logs.InsertOneAsync(new RunLogDocument
        {
            Id = $"{runId:N}:00000000",
            RunId = runId,
            ChunkNumber = 0,
            LineCount = lines.Count,
            BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
            Lines = lines,
            LineSequences = [0, 1, 2],
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);
        var stored = await logs.Find(l => l.RunId == runId).ToListAsync();
        stored.Should().NotBeEmpty();
        stored.Should().OnlyContain(l => l.BsonByteCount <= MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes);
        foreach (var chunk in stored)
            Encoding.UTF8.GetByteCount(chunk.ToBsonDocument().ToJson()).Should().BeLessThanOrEqualTo(SnapshotPartitioner.HardCapBytes);

        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(lines);

        await store.AppendLogsAsync(runId, [new string('z', SnapshotPartitioner.HardCapBytes)], CancellationToken.None);
        var after = await store.GetLogsAsync(runId, CancellationToken.None);
        after.Should().HaveCount(lines.Count + 1);
        after[^1].Should().EndWith(" [truncated: exceeded log chunk byte budget]");
        SnapshotPartitioner.EscapedContentBytes(after[^1]).Should().Be(
            MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes - MongoSnapshotStore.EstimatedBsonBytesPerLineOverhead);
    }

    [Fact]
    public async Task Split_resumes_from_the_saved_start_without_duplicating_lines()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var lines = Enumerable.Range(0, 3).Select(i => new string((char)('a' + i), 400_000)).ToList();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var sourceId = $"{runId:N}:00000000";
        await logs.InsertOneAsync(new RunLogDocument
        {
            Id = sourceId,
            RunId = runId,
            ChunkNumber = 0,
            LineCount = lines.Count,
            BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
            Lines = lines,
            LineSequences = [],
            SplitStart = 1,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await logs.InsertOneAsync(new RunLogDocument
        {
            Id = $"{runId:N}:00000001",
            RunId = runId,
            ChunkNumber = 1,
            LineCount = 1,
            BsonByteCount = 400_000,
            Lines = [lines[0]],
            LineSequences = [0],
            SplitFromId = sourceId,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);

        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(lines);
        var stored = await logs.Find(l => l.RunId == runId).ToListAsync();
        stored.Should().NotContain(l => l.Id == sourceId);
        stored.SelectMany(l => l.Lines).Should().Equal(lines);
    }

    [Fact]
    public async Task Migration_translates_a_large_snapshot_sweeps_orphans_and_can_resume()
    {
        var database = CreateGuardedDatabase();
        var store = new MongoSnapshotStore(database, NullLogger<MongoSnapshotStore>.Instance);
        var snapshots = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var parts = _fixture.Database.GetCollection<SnapshotPartDocument>(SnapshotPartDocument.CollectionName);
        var runId = Guid.NewGuid();
        var payload = Enumerable.Range(0, 20).Select(i => new Item(i.ToString(), new string('m', 60_000))).ToList();
        await snapshots.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "measureResources",
            Data = JsonSerializerPayload(payload),
            UpdatedAt = DateTimeOffset.UtcNow
        });
        var activeRunId = Guid.NewGuid();
        var orphanRunId = Guid.NewGuid();
        await parts.InsertManyAsync(
        [
            new SnapshotPartDocument
            {
                Id = "orphan-part",
                RunId = orphanRunId,
                Domain = "entries",
                GenerationId = "gone",
                Ordinal = 0,
                Kind = "element",
                Path = "$",
                Data = "{}",
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-20)
            },
            new SnapshotPartDocument
            {
                Id = "orphan-part-2",
                RunId = orphanRunId,
                Domain = "entries",
                GenerationId = "gone",
                Ordinal = 1,
                Kind = "element",
                Path = "$",
                Data = "{}",
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-20)
            }
        ]);
        await parts.InsertOneAsync(new SnapshotPartDocument
        {
            Id = "active-old",
            RunId = activeRunId,
            Domain = "entries",
            GenerationId = "still-writing",
            Ordinal = 0,
            Kind = "element",
            Path = "$",
            Data = "{}",
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-20)
        });
        await parts.InsertOneAsync(new SnapshotPartDocument
        {
            Id = "active-fresh",
            RunId = activeRunId,
            Domain = "entries",
            GenerationId = "still-writing",
            Ordinal = 1,
            Kind = "element",
            Path = "$",
            Data = "{}",
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var service = new SnapshotShapeMigrationService(
            database,
            store,
            NullLogger<SnapshotShapeMigrationService>.Instance);
        await service.StartAsync(CancellationToken.None);
        var migrated = false;
        for (var i = 0; i < 60 && !migrated; i++)
        {
            await Task.Delay(500);
            var header = await snapshots.Find(s => s.RunId == runId && s.Domain == "measureResources").FirstOrDefaultAsync();
            var orphan = await parts.Find(p => p.Id == "orphan-part").FirstOrDefaultAsync();
            migrated = header != null
                && header.Data.Contains(SnapshotPartitioner.ShapeProperty, StringComparison.Ordinal)
                && orphan == null
            && await parts.Find(p => p.Id == "orphan-part-2").FirstOrDefaultAsync() == null;
        }

        await service.StopAsync(CancellationToken.None);
        migrated.Should().BeTrue();
        (await parts.Find(p => p.Id == "active-old").AnyAsync()).Should().BeTrue();
        (await parts.Find(p => p.Id == "active-fresh").AnyAsync()).Should().BeTrue();

        var read = await store.GetDomainAsync<List<Item>>(runId, "measureResources", CancellationToken.None);
        read!.Data.Should().Equal(payload);
        var again = await snapshots.Find(s => s.RunId == runId && s.Domain == "measureResources").FirstAsync();
        (await store.TryUpgradeLegacyDomainAsync(again, CancellationToken.None)).Should().BeFalse();
        await AssertRunDocumentsFit(runId);
    }

    [Fact]
    public async Task Guard_rejects_a_document_write_over_two_megabytes()
    {
        var database = CreateGuardedDatabase();
        var parts = database.GetCollection<SnapshotPartDocument>(SnapshotPartDocument.CollectionName);
        var oversized = new SnapshotPartDocument
        {
            Id = "too-big",
            RunId = Guid.NewGuid(),
            Domain = "acquisitionLogs",
            GenerationId = "g",
            Ordinal = 0,
            Kind = "element",
            Path = "$",
            Data = new string('a', SnapshotPartitioner.HardCapBytes),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var act = () => parts.InsertOneAsync(oversized);
        await act.Should().ThrowAsync<SnapshotDocumentTooLargeException>();
        var stored = await _fixture.Database
            .GetCollection<SnapshotPartDocument>(SnapshotPartDocument.CollectionName)
            .Find(p => p.Id == "too-big")
            .FirstOrDefaultAsync();
        stored.Should().BeNull();
    }

    [Fact]
    public async Task Guard_rejects_an_update_that_grows_a_document_over_two_megabytes()
    {
        var database = CreateGuardedDatabase();
        var logs = database.GetCollection<RunLogDocument>("automation_logs");
        var id = "grow-me";
        var line = new string('a', 1_600_000);
        await logs.InsertOneAsync(new RunLogDocument
        {
            Id = id,
            RunId = Guid.NewGuid(),
            ChunkNumber = 0,
            LineCount = 1,
            BsonByteCount = line.Length,
            ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
            Lines = [line],
            LineSequences = [0],
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var pushed = new string('b', 500_000);
        var act = () => logs.UpdateOneAsync(
            l => l.Id == id,
            Builders<RunLogDocument>.Update.Push(l => l.Lines, pushed));
        await act.Should().ThrowAsync<SnapshotDocumentTooLargeException>();

        var bulk = () => logs.BulkWriteAsync(
        [
            new UpdateOneModel<RunLogDocument>(
                Builders<RunLogDocument>.Filter.Eq(l => l.Id, id),
                Builders<RunLogDocument>.Update.Push(l => l.Lines, pushed))
        ]);
        await bulk.Should().ThrowAsync<SnapshotDocumentTooLargeException>();

        var stored = await _fixture.Database.GetCollection<RunLogDocument>("automation_logs")
            .Find(l => l.Id == id)
            .FirstAsync();
        stored.Lines.Should().ContainSingle().Which.Should().Be(line);
    }

    [Fact]
    public async Task Guard_rejects_a_bulk_write_of_two_full_size_parts()
    {
        var database = CreateGuardedDatabase();
        var parts = database.GetCollection<SnapshotPartDocument>(SnapshotPartDocument.CollectionName);
        var payload = new string('a', 1_100_000);
        var writes = new WriteModel<SnapshotPartDocument>[]
        {
            Replacement("bulk-a", payload),
            Replacement("bulk-b", payload)
        };

        var act = () => parts.BulkWriteAsync(writes);
        await act.Should().ThrowAsync<SnapshotDocumentTooLargeException>();
    }

    [Fact]
    public async Task Guard_rejects_an_oversized_replacement()
    {
        var database = CreateGuardedDatabase();
        var parts = database.GetCollection<SnapshotPartDocument>(SnapshotPartDocument.CollectionName);
        var oversized = new SnapshotPartDocument
        {
            Id = "replaced-too-big",
            RunId = Guid.NewGuid(),
            Domain = "acquisitionLogs",
            GenerationId = "g",
            Ordinal = 0,
            Kind = "element",
            Path = "$",
            Data = new string('a', SnapshotPartitioner.HardCapBytes),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var act = () => parts.ReplaceOneAsync(p => p.Id == oversized.Id, oversized);
        await act.Should().ThrowAsync<SnapshotDocumentTooLargeException>();
    }

    [Fact]
    public async Task SetDomainAsync_stores_full_size_slices_without_one_oversized_request()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var note = new string('z', SnapshotPartitioner.MaxDocumentJsonBytes * 2);

        await store.SetDomainAsync(runId, "generationManifest", new Item("only", note), CancellationToken.None);

        var read = await store.GetDomainAsync<Item>(runId, "generationManifest", CancellationToken.None);
        read!.Data.Note.Should().Be(note);
        var parts = await Parts(runId, "generationManifest");
        parts.Should().HaveCountGreaterThan(1);
        await AssertRunDocumentsFit(runId);
    }

    [Fact]
    public async Task TryUpgrade_leaves_a_snapshot_alone_when_its_timestamp_changed()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var snapshots = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var payload = JsonSerializerPayload(Enumerable.Range(0, 20).Select(i => new Item(i.ToString(), new string('q', 60_000))).ToList());
        await snapshots.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "populations",
            Data = payload,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var legacy = await snapshots.Find(d => d.RunId == runId && d.Domain == "populations").FirstAsync();
        legacy.UpdatedAt = legacy.UpdatedAt.AddMinutes(-5);

        (await store.TryUpgradeLegacyDomainAsync(legacy, CancellationToken.None)).Should().BeFalse();
        var stored = await snapshots.Find(d => d.RunId == runId && d.Domain == "populations").FirstAsync();
        stored.Data.Should().Be(payload);
    }

    private static ReplaceOneModel<SnapshotPartDocument> Replacement(string id, string payload)
        => new(
            Builders<SnapshotPartDocument>.Filter.Eq(p => p.Id, id),
            new SnapshotPartDocument
            {
                Id = id,
                RunId = Guid.NewGuid(),
                Domain = "acquisitionLogs",
                GenerationId = "g",
                Ordinal = 0,
                Kind = "slice",
                Path = "$",
                Data = payload,
                UpdatedAt = DateTimeOffset.UtcNow
            })
        {
            IsUpsert = true
        };

    [Fact]
    public async Task Split_keeps_the_order_of_an_earlier_unsequenced_chunk()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var firstLines = new List<string> { "first-a", "first-b" };
        var secondLines = Enumerable.Range(0, 3).Select(i => new string((char)('a' + i), 400_000)).ToList();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        await logs.InsertManyAsync(
        [
            new RunLogDocument
            {
                Id = $"{runId:N}:00000000",
                RunId = runId,
                ChunkNumber = 0,
                LineCount = firstLines.Count,
                BsonByteCount = 100,
                Lines = firstLines,
                LineSequences = [],
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new RunLogDocument
            {
                Id = $"{runId:N}:00000001",
                RunId = runId,
                ChunkNumber = 1,
                LineCount = secondLines.Count,
                BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
                Lines = secondLines,
                LineSequences = [],
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(firstLines.Concat(secondLines));
    }

    [Fact]
    public async Task GetLogs_hides_a_replacement_while_its_source_chunk_exists()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var sourceId = $"{runId:N}:00000000";
        await logs.InsertManyAsync(
        [
            new RunLogDocument
            {
                Id = sourceId,
                RunId = runId,
                ChunkNumber = 0,
                LineCount = 2,
                BsonByteCount = 20,
                ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
                Lines = ["keep-a", "keep-b"],
                LineSequences = [0, 1],
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new RunLogDocument
            {
                Id = $"{runId:N}:00000001",
                RunId = runId,
                ChunkNumber = 1,
                LineCount = 1,
                BsonByteCount = 10,
                ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
                Lines = ["dup-a"],
                LineSequences = [0],
                SplitFromId = sourceId,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal("keep-a", "keep-b");
    }

    [Fact]
    public async Task AppendLogs_recomputes_a_legacy_raw_byte_count_before_appending()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var legacyLine = new string('\u0001', 300_000);
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var legacyId = $"{runId:N}:00000000";
        await logs.InsertOneAsync(new RunLogDocument
        {
            Id = legacyId,
            RunId = runId,
            ChunkNumber = 0,
            LineCount = 1,
            BsonByteCount = Encoding.UTF8.GetByteCount(legacyLine) + 64,
            ByteCountVersion = 0,
            Lines = [legacyLine],
            LineSequences = [0],
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var appended = new string('a', 400_000);
        await store.AppendLogsAsync(runId, [appended], CancellationToken.None);

        var stored = await logs.Find(l => l.RunId == runId).ToListAsync();
        stored.Should().HaveCountGreaterThan(1);
        foreach (var chunk in stored)
            Encoding.UTF8.GetByteCount(chunk.ToBsonDocument().ToJson()).Should().BeLessThanOrEqualTo(SnapshotPartitioner.HardCapBytes);

        var legacy = stored.Single(l => l.Id == legacyId);
        legacy.Lines.Should().ContainSingle().Which.Should().Be(legacyLine);
        legacy.ByteCountVersion.Should().Be(MongoSnapshotStore.EscapedLogByteCountVersion);
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(legacyLine, appended);
    }

    [Fact]
    public async Task SetDomainAsync_collapses_duplicate_headers_onto_the_canonical_id()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var snapshots = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var canonical = MongoSnapshotStore.CanonicalSnapshotId(runId, "entries");
        var now = DateTimeOffset.UtcNow;
        await snapshots.InsertManyAsync(
        [
            new DomainSnapshotDocument
            {
                Id = canonical,
                RunId = runId,
                Domain = "entries",
                Data = "{\"Name\":\"old\"}",
                UpdatedAt = now.AddMinutes(-1)
            },
            new DomainSnapshotDocument
            {
                RunId = runId,
                Domain = "entries",
                Data = "{\"Name\":\"other\"}",
                UpdatedAt = now.AddMinutes(-2)
            }
        ]);

        await store.SetDomainAsync(runId, "entries", new LegacySchedule("new"), CancellationToken.None);

        var headers = await snapshots.Find(d => d.RunId == runId && d.Domain == "entries").ToListAsync();
        headers.Should().ContainSingle();
        headers[0].Id.Should().Be(canonical);
        var read = await store.GetDomainAsync<LegacySchedule>(runId, "entries", CancellationToken.None);
        read!.Data.Name.Should().Be("new");
    }

    [Fact]
    public async Task Append_during_a_partial_split_keeps_the_new_line()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var lines = Enumerable.Range(0, 3).Select(i => new string((char)('a' + i), 400_000)).ToList();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var sourceId = $"{runId:N}:00000000";
        await logs.InsertManyAsync(
        [
            new RunLogDocument
            {
                Id = sourceId,
                RunId = runId,
                ChunkNumber = 0,
                LineCount = lines.Count,
                BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
                Lines = lines,
                LineSequences = [],
                SplitStart = 1,
                SplitCount = 3,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new RunLogDocument
            {
                Id = $"{runId:N}:00000001",
                RunId = runId,
                ChunkNumber = 1,
                LineCount = 1,
                BsonByteCount = 400_000,
                Lines = [lines[0]],
                LineSequences = [0],
                SplitFromId = sourceId,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ]);

        await store.AppendLogsAsync(runId, ["extra-line"], CancellationToken.None);
        var during = await logs.Find(l => l.RunId == runId).ToListAsync();
        during.Single(l => l.Id == $"{runId:N}:00000001").Lines.Should().ContainSingle().Which.Should().Be(lines[0]);
        during.Should().Contain(l => l.Lines.Contains("extra-line"));

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(lines.Append("extra-line"));
    }

    [Fact]
    public async Task Split_leaves_a_fresh_claim_from_another_owner()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var lines = Enumerable.Range(0, 3).Select(i => new string((char)('a' + i), 400_000)).ToList();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var sourceId = $"{runId:N}:00000000";
        await logs.InsertOneAsync(new RunLogDocument
        {
            Id = sourceId,
            RunId = runId,
            ChunkNumber = 0,
            LineCount = lines.Count,
            BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
            Lines = lines,
            LineSequences = [0, 1, 2],
            SplitStart = 1,
            SplitCount = 3,
            SplitOwner = "other-owner",
            SplitClaimedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(0);
        (await logs.Find(l => l.Id == sourceId).AnyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Split_takes_over_a_stale_claim()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var lines = Enumerable.Range(0, 3).Select(i => new string((char)('a' + i), 400_000)).ToList();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var sourceId = $"{runId:N}:00000000";
        await logs.InsertOneAsync(new RunLogDocument
        {
            Id = sourceId,
            RunId = runId,
            ChunkNumber = 0,
            LineCount = lines.Count,
            BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
            Lines = lines,
            LineSequences = [0, 1, 2],
            SplitStart = 1,
            SplitCount = 3,
            SplitOwner = "other-owner",
            SplitClaimedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(lines);
        (await logs.Find(l => l.Id == sourceId).AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Split_keeps_a_later_unsequenced_chunk_after_the_source()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var sourceLines = Enumerable.Range(0, 3).Select(i => new string((char)('a' + i), 400_000)).ToList();
        var laterLines = new List<string> { "later-a", "later-b" };
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        await logs.InsertManyAsync(
        [
            new RunLogDocument
            {
                Id = $"{runId:N}:00000000",
                RunId = runId,
                ChunkNumber = 0,
                LineCount = sourceLines.Count,
                BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
                Lines = sourceLines,
                LineSequences = [],
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new RunLogDocument
            {
                Id = $"{runId:N}:00000001",
                RunId = runId,
                ChunkNumber = 1,
                LineCount = laterLines.Count,
                BsonByteCount = 20,
                Lines = laterLines,
                LineSequences = [],
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(sourceLines.Concat(laterLines));
    }

    [Fact]
    public async Task Split_of_the_legacy_log_does_not_count_its_own_lines()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var sourceLines = Enumerable.Range(0, 3).Select(i => new string((char)('a' + i), 400_000)).ToList();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        await logs.InsertManyAsync(
        [
            new RunLogDocument
            {
                Id = runId.ToString(),
                RunId = runId,
                ChunkNumber = 0,
                LineCount = sourceLines.Count,
                BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
                Lines = sourceLines,
                LineSequences = [],
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new RunLogDocument
            {
                Id = $"{runId:N}:00000000",
                RunId = runId,
                ChunkNumber = 0,
                LineCount = 1,
                BsonByteCount = 10,
                Lines = ["later"],
                LineSequences = [3],
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(sourceLines.Append("later"));
    }

    [Fact]
    public async Task Append_clears_a_split_marker_when_the_source_chunk_is_gone()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        await logs.InsertOneAsync(new RunLogDocument
        {
            Id = $"{runId:N}:00000000",
            RunId = runId,
            ChunkNumber = 0,
            LineCount = 1,
            BsonByteCount = 80,
            ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
            Lines = ["kept"],
            LineSequences = [0],
            SplitFromId = "missing-source",
            UpdatedAt = DateTimeOffset.UtcNow
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await store.AppendLogsAsync(runId, ["extra"], timeout.Token);

        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal("kept", "extra");
        var stored = await logs.Find(l => l.RunId == runId).SingleAsync();
        stored.SplitFromId.Should().BeNull();
    }

    [Fact]
    public async Task Split_does_not_stamp_sequences_on_a_later_run()
    {
        var store = CreateGuardedStore();
        Guid runId;
        Guid otherRunId;
        do
        {
            runId = Guid.NewGuid();
            otherRunId = Guid.NewGuid();
        }
        while (string.Compare(otherRunId.ToString("N"), runId.ToString("N"), StringComparison.Ordinal) <= 0);

        var sourceLines = Enumerable.Range(0, 3).Select(i => new string((char)('a' + i), 400_000)).ToList();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var otherId = $"{otherRunId:N}:00000000";
        await logs.InsertManyAsync(
        [
            new RunLogDocument
            {
                Id = $"{runId:N}:00000000",
                RunId = runId,
                ChunkNumber = 0,
                LineCount = sourceLines.Count,
                BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
                Lines = sourceLines,
                LineSequences = [],
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new RunLogDocument
            {
                Id = otherId,
                RunId = otherRunId,
                ChunkNumber = 0,
                LineCount = 1,
                BsonByteCount = 10,
                Lines = ["other"],
                LineSequences = [],
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);
        var other = await logs.Find(l => l.Id == otherId).SingleAsync();
        (other.LineSequences ?? []).Should().BeEmpty();
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(sourceLines);
    }

    [Fact]
    public async Task TryUpgrade_does_not_replace_a_newer_header()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var snapshots = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var stale = JsonSerializerPayload(Enumerable.Range(0, 20).Select(i => new Item(i.ToString(), new string('q', 60_000))).ToList());
        var current = JsonSerializerPayload(new LegacySchedule("current"));
        await snapshots.InsertManyAsync(
        [
            new DomainSnapshotDocument
            {
                RunId = runId,
                Domain = "schedule",
                Data = stale,
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
            },
            new DomainSnapshotDocument
            {
                RunId = runId,
                Domain = "schedule",
                Data = current,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var older = await snapshots.Find(d => d.RunId == runId && d.Domain == "schedule").SortBy(d => d.UpdatedAt).FirstAsync();
        (await store.TryUpgradeLegacyDomainAsync(older, CancellationToken.None)).Should().BeFalse();
        var remaining = await snapshots.Find(d => d.RunId == runId && d.Domain == "schedule").ToListAsync();
        remaining.Should().ContainSingle();
        remaining[0].Data.Should().Be(current);
        var read = await store.GetDomainAsync<LegacySchedule>(runId, "schedule", CancellationToken.None);
        read!.Data.Name.Should().Be("current");
    }

    [Fact]
    public async Task Split_keeps_following_lines_after_explicit_source_sequences()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var sourceLines = Enumerable.Range(0, 3).Select(i => new string((char)('a' + i), 400_000)).ToList();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        await logs.InsertManyAsync(
        [
            new RunLogDocument
            {
                Id = $"{runId:N}:00000000",
                RunId = runId,
                ChunkNumber = 0,
                LineCount = sourceLines.Count,
                BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
                Lines = sourceLines,
                LineSequences = [10, 11, 12],
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new RunLogDocument
            {
                Id = $"{runId:N}:00000001",
                RunId = runId,
                ChunkNumber = 1,
                LineCount = 1,
                BsonByteCount = 10,
                Lines = ["after"],
                LineSequences = [],
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(sourceLines.Append("after"));
    }

    [Fact]
    public async Task Split_keeps_order_across_consecutive_unsequenced_chunks()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var first = new[] { new string('a', 600_000), new string('b', 600_000) };
        var second = new[] { new string('c', 600_000), new string('d', 600_000) };
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        await logs.InsertManyAsync(
        [
            OversizedChunk(runId, 0, first),
            OversizedChunk(runId, 1, second)
        ]);

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(2);
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(first.Concat(second));
    }

    [Fact]
    public async Task Split_reconciles_a_partial_copy_when_the_source_changes_after_the_claim()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var sourceLines = new List<string> { new string('a', 600_000), new string('b', 600_000) };
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var source = OversizedChunk(runId, 0, sourceLines);
        source.SplitStart = 5;
        source.SplitCount = 2;
        var copy = new RunLogDocument
        {
            Id = $"{runId:N}:00000005",
            RunId = runId,
            ChunkNumber = 5,
            LineCount = 1,
            BsonByteCount = 600_000,
            ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
            Lines = [sourceLines[0]],
            LineSequences = [0],
            SplitFromId = source.Id,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await logs.InsertManyAsync([source, copy]);

        var appended = false;
        MongoSnapshotStore.AfterLogSplitClaimedForTests = _ =>
        {
            if (appended)
                return Task.CompletedTask;

            appended = true;
            var changed = sourceLines.Append("appended-after-claim").ToList();
            return logs.UpdateOneAsync(
                l => l.Id == source.Id,
                Builders<RunLogDocument>.Update
                    .Set(l => l.Lines, changed)
                    .Set(l => l.LineCount, changed.Count)
                    .Set(l => l.BsonByteCount, MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1));
        };

        try
        {
            var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
            split.Should().Be(1);
        }
        finally
        {
            MongoSnapshotStore.AfterLogSplitClaimedForTests = null;
        }

        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(sourceLines[0], sourceLines[1], "appended-after-claim");
        var stored = await logs.Find(l => l.RunId == runId).ToListAsync();
        stored.SelectMany(l => l.Lines).Should().Equal(sourceLines[0], sourceLines[1], "appended-after-claim");
    }

    [Fact]
    public async Task Reads_use_the_newest_header_past_the_first_eight()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var snapshots = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var now = DateTimeOffset.UtcNow;
        var documents = new List<DomainSnapshotDocument>();
        for (var i = 0; i < 8; i++)
        {
            documents.Add(new DomainSnapshotDocument
            {
                RunId = runId,
                Domain = "schedule",
                Data = JsonSerializerPayload(new LegacySchedule($"old-{i}")),
                UpdatedAt = now.AddMinutes(-30 + i)
            });
        }

        documents.Add(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "schedule",
            Data = JsonSerializerPayload(new LegacySchedule("newest")),
            UpdatedAt = now
        });
        await snapshots.InsertManyAsync(documents);

        var read = await store.GetDomainAsync<LegacySchedule>(runId, "schedule", CancellationToken.None);
        read!.Data.Name.Should().Be("newest");
    }

    [Fact]
    public async Task Split_moves_a_near_cap_legacy_chunk_without_growing_it()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var source = LargestTwoLineChunkUnderTheCap(runId);
        MongoSnapshotStore.LogMetadataFits(source).Should().BeFalse();
        await logs.InsertOneAsync(source);

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(source.Lines);
        (await logs.Find(l => l.Id == source.Id).AnyAsync()).Should().BeFalse();
        var claims = _fixture.Database.GetCollection<LogSplitClaimDocument>(LogSplitClaimDocument.CollectionName);
        (await claims.Find(c => c.Id == source.Id).AnyAsync()).Should().BeFalse();
        var stored = await logs.Find(l => l.RunId == runId).ToListAsync();
        stored.Should().NotBeEmpty();
        stored.Should().OnlyContain(l => l.ByteCountVersion == MongoSnapshotStore.EscapedLogByteCountVersion);
    }

    [Fact]
    public async Task Sweep_keeps_parts_committed_before_the_delete()
    {
        var database = CreateGuardedDatabase();
        var store = new MongoSnapshotStore(database, NullLogger<MongoSnapshotStore>.Instance);
        var snapshots = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var parts = _fixture.Database.GetCollection<SnapshotPartDocument>(SnapshotPartDocument.CollectionName);
        var runId = Guid.NewGuid();
        const string generation = "committed-late";
        await snapshots.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "entries",
            Data = "{\"name\":\"old\"}",
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1)
        });
        await parts.InsertManyAsync(
        [
            AgedPart(runId, generation, 0),
            AgedPart(runId, generation, 1)
        ]);

        var service = new SnapshotShapeMigrationService(
            database,
            store,
            NullLogger<SnapshotShapeMigrationService>.Instance);
        service.BeforeOrphanPartDelete = _ => snapshots.UpdateOneAsync(
            d => d.RunId == runId && d.Domain == "entries",
            Builders<DomainSnapshotDocument>.Update
                .Set(d => d.Data, SnapshotPartitioner.BuildHeaderJson(generation, "records", 2, null))
                .Set(d => d.UpdatedAt, DateTimeOffset.UtcNow));

        var swept = await service.SweepOrphanPartsAsync(CancellationToken.None);
        swept.Should().Be(0);
        (await parts.Find(p => p.RunId == runId).CountDocumentsAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Split_keeps_an_appended_line_when_its_text_matches_a_later_source_line()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var source = new RunLogDocument
        {
            Id = $"{runId:N}:00000000",
            RunId = runId,
            ChunkNumber = 0,
            LineCount = 2,
            BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
            ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
            Lines = ["alpha", "beta"],
            LineSequences = [0, 1],
            SplitStart = 0,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var appended = new RunLogDocument
        {
            Id = $"{runId:N}:00000005",
            RunId = runId,
            ChunkNumber = 5,
            LineCount = 2,
            BsonByteCount = 64,
            ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
            Lines = ["alpha", "beta"],
            LineSequences = [0, 4],
            SplitFromId = source.Id,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await logs.InsertManyAsync([source, appended]);

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);

        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal("alpha", "beta", "beta");
        var kept = await logs.Find(l => l.LineSequences.Contains(4)).SingleAsync();
        kept.Lines.Should().Equal("beta");
        kept.SplitFromId.Should().BeNull();
    }

    [Fact]
    public async Task Split_keeps_an_appended_line_when_the_source_has_no_sequences()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var source = new RunLogDocument
        {
            Id = $"{runId:N}:00000000",
            RunId = runId,
            ChunkNumber = 0,
            LineCount = 2,
            BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
            ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
            Lines = ["alpha", "beta"],
            LineSequences = [],
            SplitStart = 0,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await logs.InsertManyAsync(
        [
            source,
            new RunLogDocument
            {
                Id = $"{runId:N}:00000005",
                RunId = runId,
                ChunkNumber = 5,
                LineCount = 2,
                BsonByteCount = 64,
                ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
                Lines = ["alpha", "beta"],
                LineSequences = [0, 4],
                SplitFromId = source.Id,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal("alpha", "beta", "beta");
    }

    [Fact]
    public async Task Split_keeps_an_appended_line_when_the_source_sequence_list_is_partial()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var source = new RunLogDocument
        {
            Id = $"{runId:N}:00000000",
            RunId = runId,
            ChunkNumber = 0,
            LineCount = 2,
            BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
            ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
            Lines = ["alpha", "beta"],
            LineSequences = [0],
            SplitStart = 0,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await logs.InsertManyAsync(
        [
            source,
            new RunLogDocument
            {
                Id = $"{runId:N}:00000005",
                RunId = runId,
                ChunkNumber = 5,
                LineCount = 2,
                BsonByteCount = 64,
                ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
                Lines = ["alpha", "beta"],
                LineSequences = [0, 4],
                SplitFromId = source.Id,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal("alpha", "beta", "beta");
    }

    [Fact]
    public async Task Split_drops_replacements_when_the_run_is_deleted_before_insert()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        await logs.InsertOneAsync(new RunLogDocument
        {
            Id = $"{runId:N}:00000000",
            RunId = runId,
            ChunkNumber = 0,
            LineCount = 2,
            BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
            ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
            Lines = ["alpha", "beta"],
            LineSequences = [0, 1],
            UpdatedAt = DateTimeOffset.UtcNow
        });

        MongoSnapshotStore.BeforeLogReplacementInsertForTests = _ => store.DeleteRunAsync(runId, CancellationToken.None);
        try
        {
            var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
            split.Should().Be(0);
        }
        finally
        {
            MongoSnapshotStore.BeforeLogReplacementInsertForTests = null;
        }

        (await logs.Find(l => l.RunId == runId).AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Split_stops_scanning_after_a_clean_pass()
    {
        var store = CreateGuardedStore();
        store.LogRepairCaughtUp.Should().BeFalse();
        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(0);
        store.LogRepairCaughtUp.Should().BeTrue();
    }

    [Fact]
    public async Task TryUpgrade_rewrites_a_header_whose_parts_were_swept()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var snapshots = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var parts = _fixture.Database.GetCollection<SnapshotPartDocument>(SnapshotPartDocument.CollectionName);
        var payload = Enumerable.Range(0, 20).Select(i => new Item(i.ToString(), new string('q', 60_000))).ToList();
        await snapshots.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "populations",
            Data = JsonSerializerPayload(payload),
            UpdatedAt = DateTimeOffset.UtcNow
        });
        var legacy = await snapshots.Find(d => d.RunId == runId && d.Domain == "populations").FirstAsync();
        var sweptOnce = false;
        MongoSnapshotStore.BeforePartitionHeaderPublishForTests = _ =>
        {
            if (sweptOnce)
                return Task.CompletedTask;

            sweptOnce = true;
            return parts.DeleteManyAsync(p => p.RunId == runId);
        };

        try
        {
            (await store.TryUpgradeLegacyDomainAsync(legacy, CancellationToken.None)).Should().BeTrue();
        }
        finally
        {
            MongoSnapshotStore.BeforePartitionHeaderPublishForTests = null;
        }

        sweptOnce.Should().BeTrue();
        var read = await store.GetDomainAsync<List<Item>>(runId, "populations", CancellationToken.None);
        read.Should().NotBeNull();
        read!.Data.Should().Equal(payload);
    }

    [Fact]
    public async Task TryUpgrade_leaves_a_newer_header_after_a_swept_attempt()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var snapshots = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var parts = _fixture.Database.GetCollection<SnapshotPartDocument>(SnapshotPartDocument.CollectionName);
        var payload = JsonSerializerPayload(Enumerable.Range(0, 20).Select(i => new Item(i.ToString(), new string('q', 60_000))).ToList());
        await snapshots.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "populations",
            Data = payload,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        var legacy = await snapshots.Find(d => d.RunId == runId && d.Domain == "populations").FirstAsync();
        var phase = 0;
        MongoSnapshotStore.BeforePartitionHeaderPublishForTests = _ =>
        {
            phase++;
            if (phase == 1)
                return parts.DeleteManyAsync(p => p.RunId == runId);

            return snapshots.UpdateOneAsync(
                d => d.RunId == runId && d.Domain == "populations",
                Builders<DomainSnapshotDocument>.Update
                    .Set(d => d.Data, "{\"name\":\"newer\"}")
                    .Set(d => d.UpdatedAt, DateTimeOffset.UtcNow.AddMinutes(5)));
        };

        try
        {
            (await store.TryUpgradeLegacyDomainAsync(legacy, CancellationToken.None)).Should().BeFalse();
        }
        finally
        {
            MongoSnapshotStore.BeforePartitionHeaderPublishForTests = null;
        }

        var stored = await snapshots.Find(d => d.RunId == runId && d.Domain == "populations").FirstAsync();
        stored.Data.Should().Be("{\"name\":\"newer\"}");
    }

    [Fact]
    public async Task Split_drops_a_pure_middle_copy_of_the_source()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var source = new RunLogDocument
        {
            Id = $"{runId:N}:00000000",
            RunId = runId,
            ChunkNumber = 0,
            LineCount = 3,
            BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
            ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
            Lines = ["alpha", "beta", "gamma"],
            LineSequences = [0, 1, 2],
            SplitStart = 0,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await logs.InsertManyAsync(
        [
            source,
            new RunLogDocument
            {
                Id = $"{runId:N}:00000005",
                RunId = runId,
                ChunkNumber = 5,
                LineCount = 2,
                BsonByteCount = 64,
                ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
                Lines = ["beta", "gamma"],
                LineSequences = [1, 2],
                SplitFromId = source.Id,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    public async Task Split_keeps_order_across_consecutive_near_cap_chunks()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var first = LargestTwoLineChunkUnderTheCap(runId, 0, 'a', 'b');
        var second = LargestTwoLineChunkUnderTheCap(runId, 1, 'c', 'd');
        MongoSnapshotStore.LogMetadataFits(first).Should().BeFalse();
        MongoSnapshotStore.LogMetadataFits(second).Should().BeFalse();
        await logs.InsertManyAsync([first, second]);

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(2);
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal([.. first.Lines, .. second.Lines]);
        (await logs.Find(l => l.Id == first.Id || l.Id == second.Id).AnyAsync()).Should().BeFalse();
        var stamps = _fixture.Database.GetCollection<LogSequenceStampDocument>(LogSequenceStampDocument.CollectionName);
        (await stamps.CountDocumentsAsync(Builders<LogSequenceStampDocument>.Filter.Empty)).Should().Be(0);
    }

    [Fact]
    public async Task Split_selects_a_legacy_chunk_whose_stored_byte_count_is_under_the_budget()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var source = LargestTwoLineChunkUnderTheCap(runId);
        source.BsonByteCount = 10;
        source.ByteCountVersion = 0;
        MongoSnapshotStore.LogMetadataFits(source).Should().BeFalse();
        await logs.InsertOneAsync(source);

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);
        (await logs.Find(l => l.Id == source.Id).AnyAsync()).Should().BeFalse();
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(source.Lines);
    }

    [Fact]
    public async Task Append_rolls_a_near_cap_legacy_chunk_onto_a_new_document()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var source = LargestTwoLineChunkUnderTheCap(runId);
        source.BsonByteCount = 10;
        source.ByteCountVersion = 0;
        MongoSnapshotStore.LogMetadataFits(source).Should().BeFalse();
        await logs.InsertOneAsync(source);

        await store.AppendLogsAsync(runId, ["tail"], CancellationToken.None);

        var stored = await logs.Find(l => l.Id == source.Id).SingleAsync();
        stored.Lines.Should().Equal(source.Lines);
        stored.ByteCountVersion.Should().Be(0);
        stored.BsonByteCount.Should().Be(10);
        stored.LineCount.Should().Be(2);
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal([.. source.Lines, "tail"]);
    }

    [Fact]
    public async Task Sweep_reads_one_header_for_a_committed_generation()
    {
        var database = CreateGuardedDatabase();
        var store = new MongoSnapshotStore(database, NullLogger<MongoSnapshotStore>.Instance);
        var snapshots = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var parts = _fixture.Database.GetCollection<SnapshotPartDocument>(SnapshotPartDocument.CollectionName);
        var runId = Guid.NewGuid();
        const string generation = "committed";
        await snapshots.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "entries",
            Data = SnapshotPartitioner.BuildHeaderJson(generation, "records", 4, null),
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await parts.InsertManyAsync(
        [
            AgedPart(runId, generation, 0),
            AgedPart(runId, generation, 1),
            AgedPart(runId, generation, 2),
            AgedPart(runId, generation, 3)
        ]);

        var service = new SnapshotShapeMigrationService(
            database,
            store,
            NullLogger<SnapshotShapeMigrationService>.Instance);
        var swept = await service.SweepOrphanPartsAsync(CancellationToken.None);
        swept.Should().Be(0);
        service.PartitionHeaderReads.Should().Be(1);
        (await parts.Find(p => p.RunId == runId).CountDocumentsAsync()).Should().Be(4);
        (await parts.Find(p => p.RunId == runId && p.Settled).CountDocumentsAsync()).Should().Be(4);

        var again = await service.SweepOrphanPartsAsync(CancellationToken.None);
        again.Should().Be(0);
        service.PartitionHeaderReads.Should().Be(1);
    }

    [Fact]
    public async Task DeleteRunAsync_removes_log_split_claims_and_sequence_stamps()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var otherRunId = Guid.NewGuid();
        var claims = _fixture.Database.GetCollection<LogSplitClaimDocument>(LogSplitClaimDocument.CollectionName);
        var stamps = _fixture.Database.GetCollection<LogSequenceStampDocument>(LogSequenceStampDocument.CollectionName);
        await claims.InsertManyAsync(
        [
            new LogSplitClaimDocument
            {
                Id = $"{runId:N}:00000000",
                Owner = "owner",
                ClaimedAt = DateTimeOffset.UtcNow,
                SplitStart = 1,
                SplitCount = 2
            },
            new LogSplitClaimDocument
            {
                Id = runId.ToString(),
                Owner = "legacy",
                ClaimedAt = DateTimeOffset.UtcNow,
                SplitStart = 9,
                SplitCount = 1
            },
            new LogSplitClaimDocument
            {
                Id = $"{otherRunId:N}:00000000",
                Owner = "other",
                ClaimedAt = DateTimeOffset.UtcNow,
                SplitStart = 4,
                SplitCount = 1
            },
            new LogSplitClaimDocument
            {
                Id = otherRunId.ToString(),
                Owner = "other-legacy",
                ClaimedAt = DateTimeOffset.UtcNow,
                SplitStart = 6,
                SplitCount = 1
            }
        ]);
        await stamps.InsertManyAsync(
        [
            new LogSequenceStampDocument
            {
                Id = $"{runId:N}:00000001",
                LineSequences = [1, 2]
            },
            new LogSequenceStampDocument
            {
                Id = runId.ToString(),
                LineSequences = [3]
            }
        ]);

        await store.DeleteRunAsync(runId, CancellationToken.None);

        (await claims.Find(c => c.Id == $"{runId:N}:00000000").AnyAsync()).Should().BeFalse();
        (await claims.Find(c => c.Id == runId.ToString()).AnyAsync()).Should().BeFalse();
        (await stamps.Find(s => s.Id == $"{runId:N}:00000001").AnyAsync()).Should().BeFalse();
        (await stamps.Find(s => s.Id == runId.ToString()).AnyAsync()).Should().BeFalse();
        (await claims.Find(c => c.Id == $"{otherRunId:N}:00000000").AnyAsync()).Should().BeTrue();
        (await claims.Find(c => c.Id == otherRunId.ToString()).AnyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Append_leaves_a_frozen_unsequenced_source_ahead_of_the_new_line()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var claims = _fixture.Database.GetCollection<LogSplitClaimDocument>(LogSplitClaimDocument.CollectionName);
        await logs.InsertManyAsync(
        [
            new RunLogDocument
            {
                Id = $"{runId:N}:00000000",
                RunId = runId,
                ChunkNumber = 0,
                LineCount = 1,
                BsonByteCount = 32,
                ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
                Lines = ["early"],
                LineSequences = [0],
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new RunLogDocument
            {
                Id = $"{runId:N}:00000001",
                RunId = runId,
                ChunkNumber = 1,
                LineCount = 2,
                BsonByteCount = 64,
                ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
                Lines = ["old-a", "old-b"],
                LineSequences = [],
                SplitStart = 8,
                SplitCount = 1,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ]);
        await claims.InsertOneAsync(new LogSplitClaimDocument
        {
            Id = runId.ToString(),
            Owner = "legacy",
            ClaimedAt = DateTimeOffset.UtcNow,
            SplitStart = 20,
            SplitCount = 2
        });

        await store.AppendLogsAsync(runId, ["new"], CancellationToken.None);

        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal("early", "old-a", "old-b", "new");
        var earlier = await logs.Find(l => l.Id == $"{runId:N}:00000000").SingleAsync();
        earlier.Lines.Should().Equal("early");
        var created = await logs.Find(l => l.Lines.Contains("new")).SingleAsync();
        created.ChunkNumber.Should().Be(22);
    }

    [Fact]
    public async Task Split_keeps_an_appended_line_when_the_saved_range_does_not_collide()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var source = new RunLogDocument
        {
            Id = $"{runId:N}:00000000",
            RunId = runId,
            ChunkNumber = 0,
            LineCount = 2,
            BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
            ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
            Lines = ["alpha", "beta"],
            LineSequences = [0, 1],
            SplitStart = 5,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await logs.InsertManyAsync(
        [
            source,
            new RunLogDocument
            {
                Id = $"{runId:N}:00000005",
                RunId = runId,
                ChunkNumber = 5,
                LineCount = 2,
                BsonByteCount = 64,
                ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
                Lines = ["alpha", "beta"],
                LineSequences = [0, 4],
                SplitFromId = source.Id,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal("alpha", "beta", "beta");
        var kept = await logs.Find(l => l.LineSequences.Contains(4)).SingleAsync();
        kept.Lines.Should().Equal("beta");
    }

    [Fact]
    public async Task Split_does_not_stamp_a_replacement_whose_earlier_source_still_exists()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var sourceId = $"{runId:N}:00000000";
        await logs.InsertManyAsync(
        [
            new RunLogDocument
            {
                Id = sourceId,
                RunId = runId,
                ChunkNumber = 0,
                LineCount = 2,
                BsonByteCount = 32,
                ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
                Lines = ["alpha", "beta"],
                LineSequences = [0, 1],
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new RunLogDocument
            {
                Id = $"{runId:N}:00000002",
                RunId = runId,
                ChunkNumber = 2,
                LineCount = 1,
                BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
                ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
                Lines = ["mid"],
                LineSequences = [2],
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new RunLogDocument
            {
                Id = $"{runId:N}:00000005",
                RunId = runId,
                ChunkNumber = 5,
                LineCount = 2,
                BsonByteCount = 64,
                ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
                Lines = ["alpha", "beta"],
                LineSequences = [0],
                SplitFromId = sourceId,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
        split.Should().Be(1);
        var replacement = await logs.Find(l => l.Id == $"{runId:N}:00000005").SingleAsync();
        replacement.LineSequences.Should().Equal(0L);
        replacement.SplitFromId.Should().Be(sourceId);
    }

    [Fact]
    public async Task Guard_rejects_a_filter_and_update_that_together_exceed_two_megabytes()
    {
        var database = CreateGuardedDatabase();
        var snapshots = database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var id = ObjectId.GenerateNewId();
        await snapshots.InsertOneAsync(new DomainSnapshotDocument
        {
            Id = id,
            RunId = Guid.NewGuid(),
            Domain = "entries",
            Data = "{}",
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var filterValue = new string('a', 1_100_000);
        var updateValue = new string('b', 1_100_000);
        var act = () => snapshots.UpdateOneAsync(
            d => d.Id == id && d.Data == filterValue,
            Builders<DomainSnapshotDocument>.Update.Set(d => d.Data, updateValue));
        await act.Should().ThrowAsync<SnapshotDocumentTooLargeException>();
        var stored = await _fixture.Database
            .GetCollection<DomainSnapshotDocument>("automation_snapshots")
            .Find(d => d.Id == id)
            .SingleAsync();
        stored.Data.Should().Be("{}");
    }

    [Fact]
    public async Task Sweep_removes_a_retired_settled_generation_and_keeps_the_live_one()
    {
        var database = CreateGuardedDatabase();
        var store = new MongoSnapshotStore(database, NullLogger<MongoSnapshotStore>.Instance);
        var snapshots = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var parts = _fixture.Database.GetCollection<SnapshotPartDocument>(SnapshotPartDocument.CollectionName);
        var retired = _fixture.Database.GetCollection<RetiredSnapshotGenerationDocument>(RetiredSnapshotGenerationDocument.CollectionName);
        var runId = Guid.NewGuid();
        await snapshots.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "entries",
            Data = SnapshotPartitioner.BuildHeaderJson("live", "records", 1, null),
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await parts.InsertManyAsync(
        [
            new SnapshotPartDocument
            {
                Id = $"{runId:N}:entries:live:00000000",
                RunId = runId,
                Domain = "entries",
                GenerationId = "live",
                Ordinal = 0,
                Kind = "element",
                Path = "$",
                Data = "{}",
                UpdatedAt = DateTimeOffset.UtcNow,
                Settled = true
            },
            new SnapshotPartDocument
            {
                Id = $"{runId:N}:entries:old:00000000",
                RunId = runId,
                Domain = "entries",
                GenerationId = "old",
                Ordinal = 0,
                Kind = "element",
                Path = "$",
                Data = "{}",
                UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                Settled = true
            }
        ]);
        await retired.InsertManyAsync(
        [
            new RetiredSnapshotGenerationDocument
            {
                Id = $"{runId:N}:entries:old",
                RunId = runId,
                Domain = "entries",
                GenerationId = "old"
            },
            new RetiredSnapshotGenerationDocument
            {
                Id = $"{runId:N}:entries:live",
                RunId = runId,
                Domain = "entries",
                GenerationId = "live"
            }
        ]);

        var service = new SnapshotShapeMigrationService(
            database,
            store,
            NullLogger<SnapshotShapeMigrationService>.Instance);
        await service.SweepOrphanPartsAsync(CancellationToken.None);

        (await parts.Find(p => p.GenerationId == "old").AnyAsync()).Should().BeFalse();
        (await parts.Find(p => p.GenerationId == "live").AnyAsync()).Should().BeTrue();
        (await retired.Find(t => t.GenerationId == "old").AnyAsync()).Should().BeFalse();
        (await retired.Find(t => t.GenerationId == "live").AnyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Inline_rewrite_retires_the_replaced_generation_when_a_newer_header_lands()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var first = Enumerable.Range(0, 20).Select(i => new Item("a" + i, new string('a', 60_000))).ToList();
        var newer = Enumerable.Range(0, 20).Select(i => new Item("b" + i, new string('b', 60_000))).ToList();
        await store.SetDomainAsync(runId, "entries", first, CancellationToken.None);
        var firstGeneration = (await Parts(runId, "entries")).Select(p => p.GenerationId).Distinct().Single();

        MongoSnapshotStore.BeforeInlineGenerationRetireForTests = async _ =>
        {
            await store.SetDomainAsync(runId, "entries", newer, CancellationToken.None);
        };
        try
        {
            await store.SetDomainAsync(runId, "entries", new Item("small", "ok"), CancellationToken.None);
        }
        finally
        {
            MongoSnapshotStore.BeforeInlineGenerationRetireForTests = null;
        }

        var parts = await Parts(runId, "entries");
        parts.Should().NotBeEmpty();
        parts.Select(p => p.GenerationId).Should().NotContain(firstGeneration);
        parts.Select(p => p.GenerationId).Distinct().Should().HaveCount(1);
        var read = await store.GetDomainAsync<List<Item>>(runId, "entries", CancellationToken.None);
        read!.Data.Should().Equal(newer);
        var retired = _fixture.Database.GetCollection<RetiredSnapshotGenerationDocument>(RetiredSnapshotGenerationDocument.CollectionName);
        (await retired.Find(t => t.RunId == runId).AnyAsync()).Should().BeFalse();
        await AssertRunDocumentsFit(runId);
    }

    [Fact]
    public async Task Sweep_removes_an_old_settled_generation_the_header_no_longer_names()
    {
        var database = CreateGuardedDatabase();
        var store = new MongoSnapshotStore(database, NullLogger<MongoSnapshotStore>.Instance);
        var snapshots = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var parts = _fixture.Database.GetCollection<SnapshotPartDocument>(SnapshotPartDocument.CollectionName);
        var runId = Guid.NewGuid();
        await snapshots.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "entries",
            Data = SnapshotPartitioner.BuildHeaderJson("live", "records", 1, null),
            UpdatedAt = DateTimeOffset.UtcNow
        });
        var live = AgedPart(runId, "live", 0);
        live.Settled = true;
        var gone = AgedPart(runId, "gone", 0);
        gone.Settled = true;
        var recent = AgedPart(runId, "recent", 0);
        recent.Settled = true;
        recent.UpdatedAt = DateTimeOffset.UtcNow;
        recent.Id = $"{runId:N}:entries:recent:00000000";
        recent.GenerationId = "recent";
        await parts.InsertManyAsync([live, gone, recent]);

        var service = new SnapshotShapeMigrationService(
            database,
            store,
            NullLogger<SnapshotShapeMigrationService>.Instance);
        await service.SweepOrphanPartsAsync(CancellationToken.None);

        (await parts.Find(p => p.GenerationId == "gone").AnyAsync()).Should().BeFalse();
        (await parts.Find(p => p.GenerationId == "live").AnyAsync()).Should().BeTrue();
        (await parts.Find(p => p.GenerationId == "recent").AnyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Sweep_advances_past_live_settled_parts_and_wraps_to_an_earlier_orphan()
    {
        var database = CreateGuardedDatabase();
        var store = new MongoSnapshotStore(database, NullLogger<MongoSnapshotStore>.Instance);
        var snapshots = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var parts = _fixture.Database.GetCollection<SnapshotPartDocument>(SnapshotPartDocument.CollectionName);
        var runId = Guid.NewGuid();
        await snapshots.InsertOneAsync(new DomainSnapshotDocument
        {
            RunId = runId,
            Domain = "entries",
            Data = SnapshotPartitioner.BuildHeaderJson("m-live", "records", 8, null),
            UpdatedAt = DateTimeOffset.UtcNow
        });
        var live = Enumerable.Range(0, 8).Select(ordinal =>
        {
            var part = AgedPart(runId, "m-live", ordinal);
            part.Settled = true;
            return part;
        }).ToList();
        var later = AgedPart(runId, "z-old", 0);
        later.Settled = true;
        await parts.InsertManyAsync(live.Append(later));

        var service = new SnapshotShapeMigrationService(
            database,
            store,
            NullLogger<SnapshotShapeMigrationService>.Instance);
        await service.SweepOrphanPartsAsync(CancellationToken.None);
        (await parts.Find(p => p.GenerationId == "z-old").AnyAsync()).Should().BeTrue();
        (await parts.Find(p => p.GenerationId == "m-live").CountDocumentsAsync()).Should().Be(8);

        await service.SweepOrphanPartsAsync(CancellationToken.None);
        (await parts.Find(p => p.GenerationId == "z-old").AnyAsync()).Should().BeFalse();

        var earlier = AgedPart(runId, "a-old", 0);
        earlier.Settled = true;
        await parts.InsertOneAsync(earlier);
        await service.SweepOrphanPartsAsync(CancellationToken.None);
        (await parts.Find(p => p.GenerationId == "a-old").AnyAsync()).Should().BeFalse();
        (await parts.Find(p => p.GenerationId == "m-live").CountDocumentsAsync()).Should().Be(8);
    }

    [Fact]
    public async Task Split_does_not_rewrite_a_successor_replacement_after_takeover()
    {
        var storeA = CreateGuardedStore();
        var storeB = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var sourceLines = new List<string> { new string('a', 600_000), new string('b', 600_000) };
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var source = OversizedChunk(runId, 0, sourceLines);
        source.ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion;
        await logs.InsertOneAsync(source);

        var replacementsWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? splitB = null;
        MongoSnapshotStore.BeforeLogSourceDeleteForTests = async ct =>
        {
            replacementsWritten.TrySetResult();
            await resumeA.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        };
        MongoSnapshotStore.BeforeLogReplacementInsertForTests = async ct =>
        {
            var withAppend = sourceLines.Append("appended-after-claim").ToList();
            await logs.UpdateOneAsync(
                l => l.Id == source.Id,
                Builders<RunLogDocument>.Update
                    .Set(l => l.Lines, withAppend)
                    .Set(l => l.LineCount, withAppend.Count)
                    .Set(l => l.BsonByteCount, MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1)
                    .Set(l => l.SplitClaimedAt, DateTimeOffset.UtcNow.AddMinutes(-10)),
                cancellationToken: ct);
            splitB = storeB.SplitOversizedLogChunksAsync(ct);
            await replacementsWritten.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        };

        try
        {
            await storeA.SplitOversizedLogChunksAsync(CancellationToken.None);
            resumeA.TrySetResult();
            splitB.Should().NotBeNull();
            await splitB!;
        }
        finally
        {
            MongoSnapshotStore.BeforeLogReplacementInsertForTests = null;
            MongoSnapshotStore.BeforeLogSourceDeleteForTests = null;
            replacementsWritten.TrySetResult();
            resumeA.TrySetResult();
        }

        var read = await storeA.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(sourceLines[0], sourceLines[1], "appended-after-claim");
    }

    [Fact]
    public async Task Split_reuses_a_saved_range_without_keeping_the_old_attempt()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var lines = new List<string> { new string('a', 600_000), new string('b', 600_000) };
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var source = OversizedChunk(runId, 0, lines);
        source.LineSequences = [0, 1];
        source.SplitStart = 1;
        source.SplitCount = 2;
        source.SplitAttempt = "crashed-attempt";
        source.SplitOwner = "crashed-owner";
        source.SplitClaimedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        source.ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion;
        await logs.InsertManyAsync(
        [
            source,
            new RunLogDocument
            {
                Id = $"{runId:N}:00000001",
                RunId = runId,
                ChunkNumber = 1,
                LineCount = 1,
                BsonByteCount = 600_000,
                ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
                Lines = [lines[0]],
                LineSequences = [0],
                SplitFromId = source.Id,
                SplitAttempt = "crashed-attempt",
                UpdatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);

        split.Should().Be(1);
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(lines);
        (await logs.Find(l => l.SplitAttempt == "crashed-attempt").AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task SetDomain_saves_when_the_resolved_header_is_deleted()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var snapshots = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        await store.SetDomainAsync(runId, "schedule", new Item("first", "old"), CancellationToken.None);
        var inlineHeader = await snapshots.Find(d => d.RunId == runId && d.Domain == "schedule").FirstAsync();
        MongoSnapshotStore.BeforeSnapshotHeaderWriteForTests = ct =>
            snapshots.DeleteOneAsync(d => d.Id == inlineHeader.Id, ct);
        try
        {
            await store.SetDomainAsync(runId, "schedule", new Item("second", "new"), CancellationToken.None);
        }
        finally
        {
            MongoSnapshotStore.BeforeSnapshotHeaderWriteForTests = null;
        }

        var inline = await store.GetDomainAsync<Item>(runId, "schedule", CancellationToken.None);
        inline!.Data.Should().Be(new Item("second", "new"));

        var entries = Enumerable.Range(0, 20).Select(i => new Item(i.ToString(), new string('e', 60_000))).ToList();
        await store.SetDomainAsync(runId, "entries", entries, CancellationToken.None);
        var entriesHeader = await snapshots.Find(d => d.RunId == runId && d.Domain == "entries").FirstAsync();
        MongoSnapshotStore.BeforeSnapshotHeaderWriteForTests = ct =>
            snapshots.DeleteOneAsync(d => d.Id == entriesHeader.Id, ct);
        try
        {
            await store.SetDomainAsync(runId, "entries", entries, CancellationToken.None);
        }
        finally
        {
            MongoSnapshotStore.BeforeSnapshotHeaderWriteForTests = null;
        }

        var partitioned = await store.GetDomainAsync<List<Item>>(runId, "entries", CancellationToken.None);
        partitioned!.Data.Should().Equal(entries);
        await AssertRunDocumentsFit(runId);
    }

    [Fact]
    public async Task Split_keeps_lines_when_reconciliation_resumes_after_takeover()
    {
        var storeA = CreateGuardedStore();
        var storeB = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var lines = Enumerable.Range(0, 3).Select(i => new string((char)('a' + i), 400_000)).ToList();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var sourceId = $"{runId:N}:00000000";
        await logs.InsertOneAsync(new RunLogDocument
        {
            Id = sourceId,
            RunId = runId,
            ChunkNumber = 0,
            LineCount = lines.Count,
            BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
            Lines = lines,
            LineSequences = [0, 1, 2],
            SplitStart = 0,
            SplitCount = 1,
            SplitOwner = "stale-owner",
            SplitClaimedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var replacementsWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reconcileFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? splitB = null;
        MongoSnapshotStore.BeforeLogSourceDeleteForTests = async ct =>
        {
            replacementsWritten.TrySetResult();
            await reconcileFinished.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        };
        MongoSnapshotStore.BeforeReplacementReconcileForTests = async ct =>
        {
            await logs.UpdateOneAsync(
                l => l.Id == sourceId,
                Builders<RunLogDocument>.Update.Set(l => l.SplitClaimedAt, DateTimeOffset.UtcNow.AddMinutes(-10)),
                cancellationToken: ct);
            splitB = storeB.SplitOversizedLogChunksAsync(ct);
            await replacementsWritten.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        };

        try
        {
            await storeA.SplitOversizedLogChunksAsync(CancellationToken.None);
            reconcileFinished.TrySetResult();
            splitB.Should().NotBeNull();
            await splitB!;
        }
        finally
        {
            MongoSnapshotStore.BeforeLogSourceDeleteForTests = null;
            MongoSnapshotStore.BeforeReplacementReconcileForTests = null;
            replacementsWritten.TrySetResult();
            reconcileFinished.TrySetResult();
        }

        var read = await storeA.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal(lines);
        (await logs.Find(l => l.Id == sourceId).AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Split_keeps_lines_when_another_worker_deletes_the_source_first()
    {
        var storeA = CreateGuardedStore();
        var storeB = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var runs = _fixture.Database.GetCollection<AutomationRunDocument>("automation_runs");
        await runs.InsertOneAsync(new AutomationRunDocument
        {
            RunId = runId,
            Status = "Running",
            CreatedAt = DateTimeOffset.UtcNow
        });
        var sourceId = $"{runId:N}:00000000";
        await logs.InsertOneAsync(new RunLogDocument
        {
            Id = sourceId,
            RunId = runId,
            ChunkNumber = 0,
            LineCount = 2,
            BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
            ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
            Lines = ["alpha", "beta"],
            LineSequences = [0, 1],
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var bPaused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var aFinishedCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var claims = 0;
        Task bTask = Task.CompletedTask;
        MongoSnapshotStore.AfterLogSplitClaimedForTests = async ct =>
        {
            if (Interlocked.Increment(ref claims) != 1)
                return;

            await logs.UpdateOneAsync(
                l => l.Id == sourceId,
                Builders<RunLogDocument>.Update.Set(l => l.SplitClaimedAt, DateTimeOffset.UtcNow.AddMinutes(-10)),
                cancellationToken: ct);
            MongoSnapshotStore.AfterLogSourceDeletedForTests = async _ =>
            {
                MongoSnapshotStore.AfterLogSourceDeletedForTests = null;
                bPaused.TrySetResult();
                await aFinishedCleanup.Task;
            };
            bTask = storeB.SplitOversizedLogChunksAsync(ct);
            var paused = await Task.WhenAny(bPaused.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            if (paused != bPaused.Task)
                throw new TimeoutException("The second worker did not pause after deleting the source.");
        };
        MongoSnapshotStore.AfterAbandonedSplitConsideredForTests = _ =>
        {
            aFinishedCleanup.TrySetResult();
            return Task.CompletedTask;
        };

        try
        {
            await storeA.SplitOversizedLogChunksAsync(CancellationToken.None);
            var finished = await Task.WhenAny(bTask, Task.Delay(TimeSpan.FromSeconds(30)));
            if (finished != bTask)
                throw new TimeoutException("The second worker did not finish publishing the replacements.");
            await bTask;
        }
        finally
        {
            MongoSnapshotStore.AfterLogSplitClaimedForTests = null;
            MongoSnapshotStore.AfterLogSourceDeletedForTests = null;
            MongoSnapshotStore.AfterAbandonedSplitConsideredForTests = null;
            bPaused.TrySetResult();
            aFinishedCleanup.TrySetResult();
        }

        (await logs.Find(l => l.Id == sourceId).AnyAsync()).Should().BeFalse();
        var read = await storeA.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal("alpha", "beta");
    }

    [Fact]
    public async Task Guard_rejects_an_update_selected_by_run_id_that_grows_past_two_megabytes()
    {
        var database = CreateGuardedDatabase();
        var logs = database.GetCollection<RunLogDocument>("automation_logs");
        var runId = Guid.NewGuid();
        var line = new string('a', 1_600_000);
        var id = $"{runId:N}:00000000";
        await logs.InsertOneAsync(new RunLogDocument
        {
            Id = id,
            RunId = runId,
            ChunkNumber = 0,
            LineCount = 1,
            BsonByteCount = line.Length,
            ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
            Lines = [line],
            LineSequences = [0],
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var act = () => logs.UpdateOneAsync(
            l => l.RunId == runId,
            Builders<RunLogDocument>.Update.Push(l => l.Lines, new string('b', 500_000)));
        await act.Should().ThrowAsync<SnapshotDocumentTooLargeException>();
        var stored = await _fixture.Database
            .GetCollection<RunLogDocument>("automation_logs")
            .Find(l => l.Id == id)
            .SingleAsync();
        stored.Lines.Should().Equal(line);
    }

    [Fact]
    public async Task Append_during_a_near_cap_split_stays_outside_the_reserved_range()
    {
        var store = CreateGuardedStore();
        var runId = Guid.NewGuid();
        var logs = _fixture.Database.GetCollection<RunLogDocument>("automation_logs");
        var source = LargestTwoLineChunkUnderTheCap(runId);
        MongoSnapshotStore.LogMetadataFits(source).Should().BeFalse();
        await logs.InsertOneAsync(source);

        var appended = false;
        MongoSnapshotStore.AfterLogSplitClaimedForTests = ct =>
        {
            if (appended)
                return Task.CompletedTask;

            appended = true;
            return store.AppendLogsAsync(runId, ["tail"], ct);
        };

        try
        {
            var split = await store.SplitOversizedLogChunksAsync(CancellationToken.None);
            split.Should().Be(1);
        }
        finally
        {
            MongoSnapshotStore.AfterLogSplitClaimedForTests = null;
        }

        appended.Should().BeTrue();
        var read = await store.GetLogsAsync(runId, CancellationToken.None);
        read.Should().Equal([.. source.Lines, "tail"]);
        (await logs.Find(l => l.Id == source.Id).AnyAsync()).Should().BeFalse();
    }

    private static RunLogDocument OversizedChunk(Guid runId, int chunkNumber, IReadOnlyList<string> lines)
        => new()
        {
            Id = $"{runId:N}:{chunkNumber:D8}",
            RunId = runId,
            ChunkNumber = chunkNumber,
            LineCount = lines.Count,
            BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
            ByteCountVersion = MongoSnapshotStore.EscapedLogByteCountVersion,
            Lines = [.. lines],
            LineSequences = [],
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static RunLogDocument LargestTwoLineChunkUnderTheCap(
        Guid runId,
        int chunkNumber = 0,
        char first = 'z',
        char second = 'z')
    {
        var low = 900_000;
        var high = 1_100_000;
        var best = low;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            var plain = Encoding.UTF8.GetByteCount(TwoLineChunk(runId, mid).ToBsonDocument().ToJson());
            if (plain <= SnapshotPartitioner.HardCapBytes)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return TwoLineChunk(runId, best, chunkNumber, first, second);
    }

    private static RunLogDocument TwoLineChunk(
        Guid runId,
        int lineLength,
        int chunkNumber = 0,
        char first = 'z',
        char second = 'z')
        => new()
        {
            Id = $"{runId:N}:{chunkNumber:D8}",
            RunId = runId,
            ChunkNumber = chunkNumber,
            LineCount = 2,
            BsonByteCount = MongoSnapshotStore.MaxLogChunkEstimatedBsonBytes + 1,
            Lines = [new string(first, lineLength), new string(second, lineLength)],
            LineSequences = [],
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static SnapshotPartDocument AgedPart(Guid runId, string generation, int ordinal)
        => new()
        {
            Id = $"{runId:N}:entries:{generation}:{ordinal:D8}",
            RunId = runId,
            Domain = "entries",
            GenerationId = generation,
            Ordinal = ordinal,
            Kind = "element",
            Path = "$",
            Data = "{}",
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-20)
        };

    private MongoSnapshotStore CreateGuardedStore()
        => new(CreateGuardedDatabase(), NullLogger<MongoSnapshotStore>.Instance);

    private IMongoDatabase CreateGuardedDatabase()
    {
        var proxy = DispatchProxy.Create<IMongoDatabase, CosmosWriteGuardDatabase>();
        ((CosmosWriteGuardDatabase)(object)proxy).Inner = _fixture.Database;
        return proxy;
    }

    private async Task<List<SnapshotPartDocument>> Parts(Guid runId, string domain)
        => await _fixture.Database
            .GetCollection<SnapshotPartDocument>(SnapshotPartDocument.CollectionName)
            .Find(p => p.RunId == runId && p.Domain == domain)
            .ToListAsync();

    private async Task AssertRunDocumentsFit(Guid runId)
    {
        var snapshots = await _fixture.Database
            .GetCollection<DomainSnapshotDocument>("automation_snapshots")
            .Find(s => s.RunId == runId)
            .ToListAsync();
        snapshots.Should().NotBeEmpty();
        foreach (var snapshot in snapshots)
            Encoding.UTF8.GetByteCount(snapshot.ToBsonDocument().ToJson())
                .Should().BeLessThanOrEqualTo(SnapshotPartitioner.HardCapBytes);

        var parts = await _fixture.Database
            .GetCollection<SnapshotPartDocument>(SnapshotPartDocument.CollectionName)
            .Find(p => p.RunId == runId)
            .ToListAsync();
        foreach (var part in parts)
            Encoding.UTF8.GetByteCount(part.ToBsonDocument().ToJson())
                .Should().BeLessThanOrEqualTo(SnapshotPartitioner.HardCapBytes);
    }

    private static string JsonSerializerPayload<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value);

    private static async Task WriteAfter(TaskCompletionSource gate, MongoSnapshotStore store, Guid runId, List<Item> payload)
    {
        await gate.Task;
        await store.SetDomainAsync(runId, "entries", payload, CancellationToken.None);
    }

    private sealed record Item(string Id, string Note);
    private sealed record LegacySchedule(string Name);
}

public class CosmosWriteGuardDatabase : DispatchProxy
{
    public IMongoDatabase? Inner { get; set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;
        if (method.IsGenericMethod && method.Name == "GetCollection")
        {
            var inner = InvokeInner(method, args);
            return CosmosWriteGuard.Wrap(method.GetGenericArguments()[0], inner!);
        }

        return InvokeInner(method, args);
    }

    private object? InvokeInner(MethodInfo method, object?[]? args)
    {
        try
        {
            return method.Invoke(Inner, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}

public class CosmosWriteGuardCollection<T> : DispatchProxy
{
    public IMongoCollection<T>? Inner { get; set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        Inspect(args);
        try
        {
            return targetMethod.Invoke(Inner, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private void Inspect(object?[]? args)
    {
        if (args == null || args.Length == 0)
            return;

        FilterDefinition<T>? filter = null;
        UpdateDefinition<T>? update = null;
        foreach (var arg in args)
        {
            switch (arg)
            {
                case T document:
                    Reject(document);
                    break;
                case IEnumerable<WriteModel<T>> writes:
                    var bulkBytes = 0;
                    foreach (var write in writes)
                    {
                        switch (write)
                        {
                            case InsertOneModel<T> insert:
                                bulkBytes += Reject(insert.Document);
                                break;
                            case ReplaceOneModel<T> replace:
                                bulkBytes += Reject(replace.Replacement);
                                break;
                            case UpdateOneModel<T> one:
                                RejectUpdate(one.Filter, one.Update);
                                break;
                            case UpdateManyModel<T> manyUpdate:
                                RejectUpdate(manyUpdate.Filter, manyUpdate.Update);
                                break;
                        }
                    }

                    RejectTotal(bulkBytes);
                    break;
                case IEnumerable<T> many:
                    var insertBytes = 0;
                    foreach (var document in many)
                        insertBytes += Reject(document);
                    RejectTotal(insertBytes);
                    break;
                case FilterDefinition<T> foundFilter:
                    filter = foundFilter;
                    break;
                case UpdateDefinition<T> foundUpdate:
                    update = foundUpdate;
                    break;
            }
        }

        if (update != null)
            RejectUpdate(filter, update);
    }

    private void RejectUpdate(FilterDefinition<T>? filter, UpdateDefinition<T>? update)
    {
        if (update == null || Inner == null)
            return;

        BsonDocument rendered;
        try
        {
            rendered = update.Render(new RenderArgs<T>(Inner.DocumentSerializer, Inner.Settings.SerializerRegistry)).AsBsonDocument;
        }
        catch (Exception)
        {
            RejectText(update.ToString());
            return;
        }

        var updateJson = rendered.ToJson();
        RejectText(updateJson);
        RejectCombined(RenderedFilterJson(filter), updateJson);
        var current = LoadMatched(filter);
        var prospective = current == null ? new BsonDocument() : current.ToBsonDocument();
        if (current == null)
            ApplySet(prospective, rendered, "$setOnInsert");

        ApplySet(prospective, rendered, "$set");
        ApplyPush(prospective, rendered, "$push");
        RejectText(prospective.ToJson());
    }

    private string RenderedFilterJson(FilterDefinition<T>? filter)
    {
        if (filter == null || Inner == null)
            return "";

        try
        {
            return filter.Render(new RenderArgs<T>(Inner.DocumentSerializer, Inner.Settings.SerializerRegistry))
                .AsBsonDocument
                .ToJson();
        }
        catch (Exception)
        {
            return filter.ToString() ?? "";
        }
    }

    private static void RejectCombined(string? filterJson, string? updateJson)
    {
        var bytes = Encoding.UTF8.GetByteCount(filterJson ?? "") + Encoding.UTF8.GetByteCount(updateJson ?? "");
        if (bytes > SnapshotPartitioner.HardCapBytes)
        {
            throw new SnapshotDocumentTooLargeException(
                $"Rejected a {bytes} byte update request. Cosmos DB for MongoDB RU allows {SnapshotPartitioner.HardCapBytes} bytes in one request.");
        }
    }

    private T? LoadMatched(FilterDefinition<T>? filter)
    {
        if (filter == null || Inner == null)
            return default;

        try
        {
            return Inner.Find(filter).Limit(1).FirstOrDefault();
        }
        catch (Exception)
        {
            return default;
        }
    }

    private static void ApplySet(BsonDocument document, BsonDocument update, string op)
    {
        if (!update.TryGetValue(op, out var value) || !value.IsBsonDocument)
            return;

        foreach (var element in value.AsBsonDocument)
            document[element.Name] = element.Value;
    }

    private static void ApplyPush(BsonDocument document, BsonDocument update, string op)
    {
        if (!update.TryGetValue(op, out var value) || !value.IsBsonDocument)
            return;

        foreach (var element in value.AsBsonDocument)
        {
            var array = document.TryGetValue(element.Name, out var existing) && existing.IsBsonArray
                ? new BsonArray(existing.AsBsonArray)
                : new BsonArray();
            if (element.Value.IsBsonDocument && element.Value.AsBsonDocument.TryGetValue("$each", out var each) && each.IsBsonArray)
            {
                foreach (var item in each.AsBsonArray)
                    array.Add(item);
            }
            else
            {
                array.Add(element.Value);
            }

            document[element.Name] = array;
        }
    }

    private static int Reject(T document)
    {
        var json = document.ToBsonDocument().ToJson();
        RejectText(json);
        return Encoding.UTF8.GetByteCount(json);
    }

    private static void RejectTotal(int bytes)
    {
        if (bytes > SnapshotPartitioner.HardCapBytes)
        {
            throw new SnapshotDocumentTooLargeException(
                $"Rejected a {bytes} byte bulk write. Cosmos DB for MongoDB RU allows {SnapshotPartitioner.HardCapBytes} bytes in one request.");
        }
    }

    private static void RejectText(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return;

        var bytes = Encoding.UTF8.GetByteCount(json);
        if (bytes > SnapshotPartitioner.HardCapBytes)
        {
            throw new SnapshotDocumentTooLargeException(
                $"Rejected a {bytes} byte document write. Cosmos DB for MongoDB RU allows {SnapshotPartitioner.HardCapBytes} bytes.");
        }
    }
}

internal static class CosmosWriteGuard
{
    public static object Wrap(Type documentType, object inner)
    {
        var collectionType = typeof(IMongoCollection<>).MakeGenericType(documentType);
        var proxyType = typeof(CosmosWriteGuardCollection<>).MakeGenericType(documentType);
        var create = typeof(DispatchProxy)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(DispatchProxy.Create)
                && m.IsGenericMethodDefinition
                && m.GetGenericArguments().Length == 2);
        var proxy = create.MakeGenericMethod(collectionType, proxyType).Invoke(null, null)!;
        proxyType.GetProperty(nameof(CosmosWriteGuardCollection<object>.Inner))!.SetValue(proxy, inner);
        return proxy;
    }
}
