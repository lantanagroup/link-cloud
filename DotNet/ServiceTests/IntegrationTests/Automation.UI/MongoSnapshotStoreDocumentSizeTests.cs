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

        RejectText(rendered.ToJson());
        var current = LoadMatched(filter);
        var prospective = current == null ? new BsonDocument() : current.ToBsonDocument();
        if (current == null)
            ApplySet(prospective, rendered, "$setOnInsert");

        ApplySet(prospective, rendered, "$set");
        ApplyPush(prospective, rendered, "$push");
        RejectText(prospective.ToJson());
    }

    private T? LoadMatched(FilterDefinition<T>? filter)
    {
        if (filter == null || Inner == null)
            return default;

        BsonDocument rendered;
        try
        {
            rendered = filter.Render(new RenderArgs<T>(Inner.DocumentSerializer, Inner.Settings.SerializerRegistry)).AsBsonDocument;
        }
        catch (Exception)
        {
            return default;
        }

        var id = FindId(rendered);
        if (id == null)
            return default;

        return Inner.Find(Builders<T>.Filter.Eq("_id", id)).Limit(1).FirstOrDefault();
    }

    private static BsonValue? FindId(BsonDocument filter)
    {
        if (filter.TryGetValue("_id", out var id))
        {
            if (id.IsBsonDocument && id.AsBsonDocument.TryGetValue("$eq", out var eq))
                return eq;
            if (!id.IsBsonDocument)
                return id;
        }

        foreach (var name in new[] { "$and", "$or" })
        {
            if (!filter.TryGetValue(name, out var items) || !items.IsBsonArray)
                continue;

            foreach (var item in items.AsBsonArray)
            {
                if (item.IsBsonDocument && FindId(item.AsBsonDocument) is { } found)
                    return found;
            }
        }

        return null;
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
