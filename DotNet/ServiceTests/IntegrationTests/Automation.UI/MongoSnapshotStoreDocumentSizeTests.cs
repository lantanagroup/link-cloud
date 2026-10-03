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
        after[^1].Length.Should().BeLessThan(SnapshotPartitioner.HardCapBytes);
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
        await parts.InsertOneAsync(new SnapshotPartDocument
        {
            Id = "orphan-part",
            RunId = Guid.NewGuid(),
            Domain = "entries",
            GenerationId = "gone",
            Ordinal = 0,
            Kind = "element",
            Path = "$",
            Data = "{}",
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-20)
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
                && orphan == null;
        }

        await service.StopAsync(CancellationToken.None);
        migrated.Should().BeTrue();

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
        Inspect(targetMethod!.Name, args);
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

    private static void Inspect(string name, object?[]? args)
    {
        if (args == null || args.Length == 0)
            return;

        if (name is "InsertOne" or "InsertOneAsync" && args[0] is T inserted)
            Reject(inserted);
        else if (name is "ReplaceOne" or "ReplaceOneAsync" && args[0] is T replaced)
            Reject(replaced);
        else if (name is "InsertMany" or "InsertManyAsync" && args[0] is IEnumerable<T> many)
        {
            foreach (var document in many)
                Reject(document);
        }
        else if (name is "BulkWrite" or "BulkWriteAsync" && args[0] is IEnumerable<WriteModel<T>> writes)
        {
            foreach (var write in writes)
            {
                switch (write)
                {
                    case InsertOneModel<T> insert:
                        Reject(insert.Document);
                        break;
                    case ReplaceOneModel<T> replace:
                        Reject(replace.Replacement);
                        break;
                    case UpdateOneModel<T> update:
                        RejectText(update.Update.ToString());
                        break;
                }
            }
        }
        else if (name is "UpdateOne" or "UpdateOneAsync" or "UpdateMany" or "UpdateManyAsync"
                 or "FindOneAndUpdate" or "FindOneAndUpdateAsync")
        {
            if (args.Length > 1)
                RejectText(args[1]?.ToString());
        }
    }

    private static void Reject(T document)
        => RejectText(document.ToBsonDocument().ToJson());

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
