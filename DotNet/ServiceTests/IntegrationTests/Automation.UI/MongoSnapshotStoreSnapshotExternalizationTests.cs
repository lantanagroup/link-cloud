using Automation.UI.Services.Persistence;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Concurrent;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.AutomationUI;

[Collection(IntegrationTestCollection.Name)]
public class MongoSnapshotStoreSnapshotExternalizationTests : IAsyncLifetime
{
    private readonly AutomationUIIntegrationTestFixture _fixture;

    public MongoSnapshotStoreSnapshotExternalizationTests(AutomationUIIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SetDomainAsync_externalizes_large_allowed_domain_and_GetDomainAsync_hydrates_from_payload_store()
    {
        var payloadStore = new FakeSnapshotPayloadStore();
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);

        var runId = Guid.NewGuid();
        var payload = new Dictionary<string, string>
        {
            ["p-0001"] = new string('x', 512)
        };

        await store.SetDomainAsync(runId, "generationManifest", payload, CancellationToken.None);

        var snapshotCollection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var stored = await snapshotCollection
            .Find(s => s.RunId == runId && s.Domain == "generationManifest")
            .FirstOrDefaultAsync();

        stored.Should().NotBeNull();
        using var storedDoc = JsonDocument.Parse(stored!.Data);
        storedDoc.RootElement.TryGetProperty("__externalSnapshotPayloadPointer", out var pointerNode).Should().BeTrue();
        var pointer = pointerNode.Deserialize<SnapshotPayloadPointer>();
        pointer.Should().NotBeNull();
        pointer!.Kind.Should().Be(SnapshotPayloadPointer.KindValue);
        pointer.BlobName.Should().NotBeNullOrWhiteSpace();

        var hydrated = await store.GetDomainAsync<Dictionary<string, string>>(runId, "generationManifest", CancellationToken.None);
        hydrated.Should().NotBeNull();
        hydrated!.Data.Should().ContainKey("p-0001");
        hydrated.Data["p-0001"].Length.Should().Be(512);
    }

    [Fact]
    public async Task GetDomainAsync_treats_inline_abs_shaped_payload_as_inline_not_external_pointer()
    {
        var payloadStore = new FakeSnapshotPayloadStore();
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);

        var runId = Guid.NewGuid();
        var inlinePayload = new AbsLikeInlinePayload
        {
            Kind = "abs",
            Blob = "patient-blob.ndjson",
            Value = "inline"
        };

        await store.SetDomainAsync(runId, "schedule", inlinePayload, CancellationToken.None);

        var snapshot = await store.GetDomainAsync<AbsLikeInlinePayload>(runId, "schedule", CancellationToken.None);
        snapshot.Should().NotBeNull();
        snapshot!.Data.Kind.Should().Be("abs");
        snapshot.Data.Blob.Should().Be("patient-blob.ndjson");
        snapshot.Data.Value.Should().Be("inline");
        payloadStore.ReadCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteRunAsync_removes_run_and_invokes_payload_store_cleanup_for_run_owned_externalized_data()
    {
        var payloadStore = new FakeSnapshotPayloadStore();
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);

        var runId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        await store.UpsertRunSummaryAsync(new AutomationRunSummary
        {
            RunId = runId,
            RunName = "Run to delete",
            Scenario = AutomationScenarioKind.Custom,
            Status = AutomationRunStatus.Succeeded,
            CreatedAt = createdAt,
            StartedAt = createdAt,
            FinishedAt = createdAt.AddMinutes(1)
        }, facilityId: "facility-1", reportId: "report-1", CancellationToken.None);

        await store.SetDomainAsync(runId, "generationManifest", new Dictionary<string, string>
        {
            ["p-1"] = new string('y', 512)
        }, CancellationToken.None);

        await store.DeleteRunAsync(runId, CancellationToken.None);

        payloadStore.DeletedRunIds.Should().Contain(runId);

        var snapshotCollection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var snapshotCount = await snapshotCollection.CountDocumentsAsync(s => s.RunId == runId);
        snapshotCount.Should().Be(0);

        var run = await store.GetRunSummaryAsync(runId, CancellationToken.None);
        run.Should().BeNull();
    }

    [Fact]
    public async Task UpdateRunMeta_and_DeleteRun_complete_when_external_payload_container_is_missing()
    {
        var listCalls = 0;
        var payloadStore = new AzureBlobSnapshotPayloadStore(
            new ImportedBundleBlobStorageSettings
            {
                ConnectionString = "UseDevelopmentStorage=true",
                BlobContainerName = "automation-snapshot-tests"
            },
            new BlobContainerClient("UseDevelopmentStorage=true", "automation-snapshot-tests"),
            listBlobs: (_, _) =>
            {
                listCalls++;
                return new ThrowingAsyncPageable<BlobItem>(new RequestFailedException(404, "Container not found"));
            },
            deleteBlob: (_, _) => Task.CompletedTask);

        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);

        var runId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        await store.UpsertRunSummaryAsync(new AutomationRunSummary
        {
            RunId = runId,
            RunName = "Run for missing-container cleanup",
            Scenario = AutomationScenarioKind.Custom,
            Status = AutomationRunStatus.Succeeded,
            CreatedAt = createdAt,
            StartedAt = createdAt,
            FinishedAt = createdAt.AddMinutes(1)
        }, facilityId: "facility-404", reportId: "report-404", CancellationToken.None);

        await store.UpdateRunMetaAsync(runId, "facility-updated", "report-updated", CancellationToken.None);
        await store.DeleteRunAsync(runId, CancellationToken.None);

        listCalls.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task SetDomainAsync_deletes_the_uploaded_blob_when_a_newer_header_wins()
    {
        var payloadStore = new FakeSnapshotPayloadStore();
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);
        var runId = Guid.NewGuid();
        var first = new Dictionary<string, string> { ["p"] = new string('x', 512) };
        await store.SetDomainAsync(runId, "generationManifest", first, CancellationToken.None);

        var collection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var stored = await collection.Find(doc => doc.RunId == runId && doc.Domain == "generationManifest").SingleAsync();
        using var storedDoc = JsonDocument.Parse(stored.Data);
        var keptBlob = storedDoc.RootElement.GetProperty("__externalSnapshotPayloadPointer").Deserialize<SnapshotPayloadPointer>()!.BlobName;
        await collection.UpdateOneAsync(
            doc => doc.Id == stored.Id,
            Builders<DomainSnapshotDocument>.Update
                .Set(doc => doc.UpdatedAt, DateTimeOffset.UtcNow.AddMinutes(5)));
        var clockId = MongoSnapshotStore.SnapshotClockId(runId, "generationManifest");
        await _fixture.Database.GetCollection<SnapshotWriteClockDocument>("automation_snapshot_clocks")
            .ReplaceOneAsync(
                c => c.Id == clockId,
                new SnapshotWriteClockDocument
                {
                    Id = clockId,
                    RunId = runId,
                    Domain = "generationManifest",
                    WriteClock = DateTimeOffset.UtcNow.AddMinutes(5)
                },
                new ReplaceOptions { IsUpsert = true });

        var second = new Dictionary<string, string> { ["p"] = new string('y', 512) };
        await store.SetDomainAsync(runId, "generationManifest", second, CancellationToken.None);

        payloadStore.DeletedBlobNames.Should().ContainSingle();
        payloadStore.DeletedBlobNames[0].Should().NotBe(keptBlob);
        payloadStore.HasBlob(keptBlob!).Should().BeTrue();
        var hydrated = await store.GetDomainAsync<Dictionary<string, string>>(runId, "generationManifest", CancellationToken.None);
        hydrated!.Data["p"].Should().Be(new string('x', 512));
    }

    [Fact]
    public async Task SetDomainAsync_deletes_the_blob_from_the_header_it_replaced()
    {
        var payloadStore = new FakeSnapshotPayloadStore();
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);
        var runId = Guid.NewGuid();

        await store.SetDomainAsync(runId, "generationManifest", new Dictionary<string, string> { ["p"] = new string('a', 512) }, CancellationToken.None);
        var collection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");
        var first = await collection.Find(doc => doc.RunId == runId && doc.Domain == "generationManifest").SingleAsync();
        using var firstDoc = JsonDocument.Parse(first.Data);
        var firstBlob = firstDoc.RootElement.GetProperty("__externalSnapshotPayloadPointer").Deserialize<SnapshotPayloadPointer>()!.BlobName;

        await store.SetDomainAsync(runId, "generationManifest", new Dictionary<string, string> { ["p"] = new string('b', 512) }, CancellationToken.None);
        var second = await collection.Find(doc => doc.RunId == runId && doc.Domain == "generationManifest").SingleAsync();
        using var secondDoc = JsonDocument.Parse(second.Data);
        var secondBlob = secondDoc.RootElement.GetProperty("__externalSnapshotPayloadPointer").Deserialize<SnapshotPayloadPointer>()!.BlobName;

        payloadStore.DeletedBlobNames.Should().ContainSingle().Which.Should().Be(firstBlob);
        payloadStore.HasBlob(firstBlob!).Should().BeFalse();
        payloadStore.HasBlob(secondBlob!).Should().BeTrue();
        var hydrated = await store.GetDomainAsync<Dictionary<string, string>>(runId, "generationManifest", CancellationToken.None);
        hydrated!.Data["p"].Should().Be(new string('b', 512));
    }

    [Fact]
    public async Task SetDomainAsync_deletes_the_blob_on_a_displaced_header()
    {
        var payloadStore = new FakeSnapshotPayloadStore();
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);
        var runId = Guid.NewGuid();
        var collection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");

        await store.SetDomainAsync(runId, "generationManifest", new Dictionary<string, string> { ["p"] = new string('a', 512) }, CancellationToken.None);
        var first = await collection.Find(doc => doc.RunId == runId && doc.Domain == "generationManifest").SingleAsync();
        using var firstDoc = JsonDocument.Parse(first.Data);
        var firstBlob = firstDoc.RootElement.GetProperty("__externalSnapshotPayloadPointer").Deserialize<SnapshotPayloadPointer>()!.BlobName;

        var secondPointer = await payloadStore.StoreAsync(runId, "generationManifest", "{\"p\":\"manual\"}", CancellationToken.None);
        var secondStored = JsonSerializer.Serialize(new Dictionary<string, SnapshotPayloadPointer?>
        {
            ["__externalSnapshotPayloadPointer"] = secondPointer
        });
        await collection.InsertOneAsync(new DomainSnapshotDocument
        {
            Id = ObjectId.GenerateNewId(),
            RunId = runId,
            Domain = "generationManifest",
            Data = secondStored,
            UpdatedAt = first.UpdatedAt
        });

        await store.SetDomainAsync(runId, "generationManifest", new Dictionary<string, string> { ["p"] = new string('c', 512) }, CancellationToken.None);

        payloadStore.HasBlob(firstBlob!).Should().BeFalse();
        payloadStore.HasBlob(secondPointer.BlobName).Should().BeFalse();
        var kept = await collection.Find(doc => doc.RunId == runId && doc.Domain == "generationManifest").SingleAsync();
        using var keptDoc = JsonDocument.Parse(kept.Data);
        var keptBlob = keptDoc.RootElement.GetProperty("__externalSnapshotPayloadPointer").Deserialize<SnapshotPayloadPointer>()!.BlobName;
        payloadStore.HasBlob(keptBlob!).Should().BeTrue();
        var hydrated = await store.GetDomainAsync<Dictionary<string, string>>(runId, "generationManifest", CancellationToken.None);
        hydrated!.Data["p"].Should().Be(new string('c', 512));
    }

    [Fact]
    public async Task SetDomainAsync_deletes_a_replaced_blob_when_a_newer_header_wins()
    {
        var payloadStore = new FakeSnapshotPayloadStore();
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);
        var runId = Guid.NewGuid();
        var collection = _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots");

        await store.SetDomainAsync(runId, "generationManifest", new Dictionary<string, string> { ["p"] = new string('a', 512) }, CancellationToken.None);
        var first = await collection.Find(doc => doc.RunId == runId && doc.Domain == "generationManifest").SingleAsync();
        using var firstDoc = JsonDocument.Parse(first.Data);
        var firstBlob = firstDoc.RootElement.GetProperty("__externalSnapshotPayloadPointer").Deserialize<SnapshotPayloadPointer>()!.BlobName;

        string? winnerBlob = null;
        store.BeforeDeleteDisplaced = async () =>
        {
            var winner = await payloadStore.StoreAsync(runId, "generationManifest", "{\"p\":\"winner\"}", CancellationToken.None);
            winnerBlob = winner.BlobName;
            var stored = JsonSerializer.Serialize(new Dictionary<string, SnapshotPayloadPointer?>
            {
                ["__externalSnapshotPayloadPointer"] = winner
            });
            await collection.InsertOneAsync(new DomainSnapshotDocument
            {
                RunId = runId,
                Domain = "generationManifest",
                Data = stored,
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(5)
            });
        };

        await store.SetDomainAsync(runId, "generationManifest", new Dictionary<string, string> { ["p"] = new string('b', 512) }, CancellationToken.None);

        winnerBlob.Should().NotBeNullOrWhiteSpace();
        payloadStore.BlobNames.Should().BeEquivalentTo(winnerBlob);
        payloadStore.DeletedBlobNames.Should().Contain(firstBlob);
        var kept = await collection.Find(doc => doc.RunId == runId && doc.Domain == "generationManifest").SingleAsync();
        using var keptDoc = JsonDocument.Parse(kept.Data);
        keptDoc.RootElement.GetProperty("__externalSnapshotPayloadPointer").Deserialize<SnapshotPayloadPointer>()!.BlobName.Should().Be(winnerBlob);
    }

    [Fact]
    public async Task SetDomainAsync_deletes_an_uploaded_blob_when_the_header_write_fails()
    {
        var payloadStore = new FakeSnapshotPayloadStore();
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);
        var runId = Guid.NewGuid();
        store.BeforeHeaderWrite = () => throw new IOException("header write failed");

        var act = () => store.SetDomainAsync(
            runId,
            "generationManifest",
            new Dictionary<string, string> { ["p"] = new string('a', 512) },
            CancellationToken.None);
        await act.Should().ThrowAsync<IOException>();

        payloadStore.BlobNames.Should().BeEmpty();
        var stored = await _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots")
            .Find(doc => doc.RunId == runId && doc.Domain == "generationManifest")
            .ToListAsync();
        stored.Should().BeEmpty();
    }

    [Fact]
    public async Task SetDomainAsync_keeps_an_uploaded_blob_when_the_header_was_published()
    {
        var payloadStore = new FakeSnapshotPayloadStore();
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);
        var runId = Guid.NewGuid();
        await store.SetDomainAsync(
            runId,
            "generationManifest",
            new Dictionary<string, string> { ["p"] = new string('a', 512) },
            CancellationToken.None);
        var firstStored = await _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots")
            .Find(doc => doc.RunId == runId && doc.Domain == "generationManifest")
            .SingleAsync();
        using var firstDoc = JsonDocument.Parse(firstStored.Data);
        var replacedBlob = firstDoc.RootElement.GetProperty("__externalSnapshotPayloadPointer").Deserialize<SnapshotPayloadPointer>()!.BlobName;
        store.AfterSingleHeaderUpdate = () => throw new IOException("ack lost");

        var act = () => store.SetDomainAsync(
            runId,
            "generationManifest",
            new Dictionary<string, string> { ["p"] = new string('b', 512) },
            CancellationToken.None);
        await act.Should().ThrowAsync<IOException>();

        var hydrated = await store.GetDomainAsync<Dictionary<string, string>>(runId, "generationManifest", CancellationToken.None);
        hydrated!.Data["p"].Should().Be(new string('b', 512));
        var stored = await _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots")
            .Find(doc => doc.RunId == runId && doc.Domain == "generationManifest")
            .SingleAsync();
        using var storedDoc = JsonDocument.Parse(stored.Data);
        var publishedBlob = storedDoc.RootElement.GetProperty("__externalSnapshotPayloadPointer").Deserialize<SnapshotPayloadPointer>()!.BlobName;
        payloadStore.HasBlob(publishedBlob!).Should().BeTrue();
        replacedBlob.Should().NotBe(publishedBlob);
        payloadStore.HasBlob(replacedBlob!).Should().BeFalse();
    }

    [Fact]
    public async Task SetDomainAsync_reclaims_a_replaced_blob_when_a_chunked_header_update_is_not_acknowledged()
    {
        var payloadStore = new FakeSnapshotPayloadStore();
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);
        var runId = Guid.NewGuid();
        var oldPointer = await payloadStore.StoreAsync(runId, "entries", "\"old\"", CancellationToken.None);
        var pointerJson = JsonSerializer.Serialize(new Dictionary<string, SnapshotPayloadPointer>
        {
            ["__externalSnapshotPayloadPointer"] = oldPointer
        });
        await _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots").InsertOneAsync(
            new DomainSnapshotDocument
            {
                RunId = runId,
                Domain = "entries",
                Data = pointerJson,
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            });
        store.AfterChunkedHeaderUpdate = () =>
        {
            store.FailNextHeaderLookup = true;
            throw new IOException("ack lost");
        };

        var replacement = new string('b', MongoSnapshotStore.SnapshotChunkBytes + 1);
        var act = () => store.SetDomainAsync(runId, "entries", replacement, CancellationToken.None);
        await act.Should().ThrowAsync<IOException>();

        payloadStore.HasBlob(oldPointer.BlobName).Should().BeFalse();
        var hydrated = await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None);
        hydrated!.Data.Should().Be(replacement);
    }

    [Fact]
    public async Task SetDomainAsync_reclaims_a_replaced_blob_when_the_displaced_lookup_fails()
    {
        var payloadStore = new FakeSnapshotPayloadStore();
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);
        var runId = Guid.NewGuid();
        await store.SetDomainAsync(
            runId,
            "generationManifest",
            new Dictionary<string, string> { ["p"] = new string('a', 512) },
            CancellationToken.None);
        var firstStored = await _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots")
            .Find(doc => doc.RunId == runId && doc.Domain == "generationManifest")
            .SingleAsync();
        using var firstDoc = JsonDocument.Parse(firstStored.Data);
        var replacedBlob = firstDoc.RootElement.GetProperty("__externalSnapshotPayloadPointer").Deserialize<SnapshotPayloadPointer>()!.BlobName;
        store.BeforeDeleteDisplaced = () =>
        {
            store.FailNextHeaderLookup = true;
            return Task.CompletedTask;
        };

        var act = () => store.SetDomainAsync(
            runId,
            "generationManifest",
            new Dictionary<string, string> { ["p"] = new string('b', 512) },
            CancellationToken.None);
        await act.Should().ThrowAsync<IOException>();

        payloadStore.HasBlob(replacedBlob!).Should().BeFalse();
        var hydrated = await store.GetDomainAsync<Dictionary<string, string>>(runId, "generationManifest", CancellationToken.None);
        hydrated!.Data["p"].Should().Be(new string('b', 512));
        var stored = await _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots")
            .Find(doc => doc.RunId == runId && doc.Domain == "generationManifest")
            .SingleAsync();
        using var storedDoc = JsonDocument.Parse(stored.Data);
        var publishedBlob = storedDoc.RootElement.GetProperty("__externalSnapshotPayloadPointer").Deserialize<SnapshotPayloadPointer>()!.BlobName;
        publishedBlob.Should().NotBe(replacedBlob);
        payloadStore.HasBlob(publishedBlob!).Should().BeTrue();
    }

    [Fact]
    public async Task SetDomainAsync_reclaims_a_replaced_blob_when_a_chunked_displaced_lookup_fails()
    {
        var payloadStore = new FakeSnapshotPayloadStore();
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);
        var runId = Guid.NewGuid();
        var oldPointer = await payloadStore.StoreAsync(runId, "entries", "\"old\"", CancellationToken.None);
        var pointerJson = JsonSerializer.Serialize(new Dictionary<string, SnapshotPayloadPointer>
        {
            ["__externalSnapshotPayloadPointer"] = oldPointer
        });
        await _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots").InsertOneAsync(
            new DomainSnapshotDocument
            {
                RunId = runId,
                Domain = "entries",
                Data = pointerJson,
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            });
        store.BeforeDeleteDisplaced = () =>
        {
            store.FailNextHeaderLookup = true;
            return Task.CompletedTask;
        };

        var replacement = new string('b', MongoSnapshotStore.SnapshotChunkBytes + 1);
        var act = () => store.SetDomainAsync(runId, "entries", replacement, CancellationToken.None);
        await act.Should().ThrowAsync<IOException>();

        payloadStore.HasBlob(oldPointer.BlobName).Should().BeFalse();
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be(replacement);
    }

    [Fact]
    public async Task SetDomainAsync_reclaims_the_replaced_blob_when_publication_cleanup_fails()
    {
        var payloadStore = new FakeSnapshotPayloadStore();
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);
        var runId = Guid.NewGuid();
        await store.SetDomainAsync(runId, "generationManifest", new Dictionary<string, string> { ["p"] = new string('a', 512) }, CancellationToken.None);
        var replacedBlob = await StoredBlobName(runId);

        store.AfterHeaderCommitted = () => throw new IOException("clock write failed");
        var act = () => store.SetDomainAsync(runId, "generationManifest", new Dictionary<string, string> { ["p"] = new string('b', 512) }, CancellationToken.None);
        await act.Should().ThrowAsync<IOException>();

        payloadStore.HasBlob(replacedBlob).Should().BeFalse();
        var hydrated = await store.GetDomainAsync<Dictionary<string, string>>(runId, "generationManifest", CancellationToken.None);
        hydrated!.Data["p"].Should().Be(new string('b', 512));
        payloadStore.HasBlob(await StoredBlobName(runId)).Should().BeTrue();
    }

    [Fact]
    public async Task SetDomainAsync_reclaims_the_replaced_blob_when_publication_cleanup_is_cancelled()
    {
        var payloadStore = new FakeSnapshotPayloadStore();
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);
        var runId = Guid.NewGuid();
        await store.SetDomainAsync(runId, "generationManifest", new Dictionary<string, string> { ["p"] = new string('a', 512) }, CancellationToken.None);
        var replacedBlob = await StoredBlobName(runId);

        store.AfterHeaderCommitted = () => throw new OperationCanceledException();
        var act = () => store.SetDomainAsync(runId, "generationManifest", new Dictionary<string, string> { ["p"] = new string('b', 512) }, CancellationToken.None);
        await act.Should().ThrowAsync<OperationCanceledException>();

        payloadStore.HasBlob(replacedBlob).Should().BeFalse();
        var hydrated = await store.GetDomainAsync<Dictionary<string, string>>(runId, "generationManifest", CancellationToken.None);
        hydrated!.Data["p"].Should().Be(new string('b', 512));
        payloadStore.HasBlob(await StoredBlobName(runId)).Should().BeTrue();
    }

    [Fact]
    public async Task SetDomainAsync_reclaims_a_replaced_blob_when_chunked_publication_cleanup_fails()
    {
        var payloadStore = new FakeSnapshotPayloadStore();
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);
        var runId = Guid.NewGuid();
        var oldPointer = await payloadStore.StoreAsync(runId, "entries", "\"old\"", CancellationToken.None);
        await SeedPointerHeader(runId, oldPointer);

        store.AfterHeaderCommitted = () => throw new IOException("clock write failed");
        var replacement = new string('b', MongoSnapshotStore.SnapshotChunkBytes + 1);
        var act = () => store.SetDomainAsync(runId, "entries", replacement, CancellationToken.None);
        await act.Should().ThrowAsync<IOException>();

        payloadStore.HasBlob(oldPointer.BlobName).Should().BeFalse();
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be(replacement);
    }

    [Fact]
    public async Task SetDomainAsync_reclaims_a_replaced_blob_when_chunked_publication_cleanup_is_cancelled()
    {
        var payloadStore = new FakeSnapshotPayloadStore();
        var store = new MongoSnapshotStore(_fixture.Database, NullLogger<MongoSnapshotStore>.Instance, payloadStore);
        var runId = Guid.NewGuid();
        var oldPointer = await payloadStore.StoreAsync(runId, "entries", "\"old\"", CancellationToken.None);
        await SeedPointerHeader(runId, oldPointer);

        store.AfterHeaderCommitted = () => throw new OperationCanceledException();
        var replacement = new string('b', MongoSnapshotStore.SnapshotChunkBytes + 1);
        var act = () => store.SetDomainAsync(runId, "entries", replacement, CancellationToken.None);
        await act.Should().ThrowAsync<OperationCanceledException>();

        payloadStore.HasBlob(oldPointer.BlobName).Should().BeFalse();
        (await store.GetDomainAsync<string>(runId, "entries", CancellationToken.None))!.Data.Should().Be(replacement);
    }

    private async Task<string> StoredBlobName(Guid runId)
    {
        var stored = await _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots")
            .Find(doc => doc.RunId == runId && doc.Domain == "generationManifest")
            .SingleAsync();
        using var doc = JsonDocument.Parse(stored.Data);
        return doc.RootElement.GetProperty("__externalSnapshotPayloadPointer").Deserialize<SnapshotPayloadPointer>()!.BlobName!;
    }

    private Task SeedPointerHeader(Guid runId, SnapshotPayloadPointer pointer)
    {
        var pointerJson = JsonSerializer.Serialize(new Dictionary<string, SnapshotPayloadPointer>
        {
            ["__externalSnapshotPayloadPointer"] = pointer
        });
        return _fixture.Database.GetCollection<DomainSnapshotDocument>("automation_snapshots").InsertOneAsync(
            new DomainSnapshotDocument
            {
                RunId = runId,
                Domain = "entries",
                Data = pointerJson,
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            });
    }

    private sealed class FakeSnapshotPayloadStore : ISnapshotPayloadStore
    {
        private readonly ConcurrentDictionary<string, string> _payloadByBlob = new(StringComparer.Ordinal);

        public List<Guid> DeletedRunIds { get; } = [];
        public List<string> DeletedBlobNames { get; } = [];
        public int ReadCount { get; private set; }
        public bool HasBlob(string blobName) => _payloadByBlob.ContainsKey(blobName);

        public IReadOnlyCollection<string> BlobNames => _payloadByBlob.Keys.ToArray();

        public bool ShouldExternalize(string domain, int payloadUtf8Bytes) =>
            string.Equals(domain, "generationManifest", StringComparison.OrdinalIgnoreCase)
            && payloadUtf8Bytes > 128;

        public Task<SnapshotPayloadPointer> StoreAsync(Guid runId, string domain, string payloadJson, CancellationToken ct = default)
        {
            var blob = $"fake/{runId:N}/{domain}/{Guid.NewGuid():N}.json";
            _payloadByBlob[blob] = payloadJson;
            return Task.FromResult(new SnapshotPayloadPointer
            {
                BlobName = blob,
                Utf8Bytes = System.Text.Encoding.UTF8.GetByteCount(payloadJson)
            });
        }

        public Task<string?> ReadAsync(SnapshotPayloadPointer pointer, CancellationToken ct = default)
        {
            ReadCount++;
            _payloadByBlob.TryGetValue(pointer.BlobName, out var payload);
            return Task.FromResult(payload);
        }

        public Task DeleteIfExistsAsync(SnapshotPayloadPointer pointer, CancellationToken ct = default)
        {
            DeletedBlobNames.Add(pointer.BlobName);
            _payloadByBlob.TryRemove(pointer.BlobName, out _);
            return Task.CompletedTask;
        }

        public Task DeleteRunPayloadsAsync(Guid runId, CancellationToken ct = default)
        {
            DeletedRunIds.Add(runId);
            var prefix = $"fake/{runId:N}/";
            foreach (var key in _payloadByBlob.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
                _payloadByBlob.TryRemove(key, out _);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingAsyncPageable<T>(RequestFailedException exception) : AsyncPageable<T> where T : notnull
    {
        public override IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => throw exception;

        public override IAsyncEnumerable<Page<T>> AsPages(string? continuationToken = null, int? pageSizeHint = null) => throw exception;
    }

    private sealed class AbsLikeInlinePayload
    {
        public string Kind { get; set; } = string.Empty;
        public string Blob { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
