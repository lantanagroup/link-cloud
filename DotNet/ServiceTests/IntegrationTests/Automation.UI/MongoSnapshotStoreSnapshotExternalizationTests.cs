using Automation.UI.Services.Persistence;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.Extensions.Logging.Abstractions;
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

    private sealed class FakeSnapshotPayloadStore : ISnapshotPayloadStore
    {
        private readonly ConcurrentDictionary<string, string> _payloadByBlob = new(StringComparer.Ordinal);

        public List<Guid> DeletedRunIds { get; } = [];
        public int ReadCount { get; private set; }

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
