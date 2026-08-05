using Automation.UI.Services.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using MongoDB.Driver;
using System.Text.Json.Nodes;
using System.Linq.Expressions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class ImportedBundleBlobMigrationServiceTests
{
    [Fact]
    public async Task MigrateInlinePayloadBatchAsync_migrates_successful_documents()
    {
        var (service, bundlesMock, contentStoreMock) = CreateService();
        var doc = new ImportedBundleDocument
        {
            Id = Guid.NewGuid(),
            ContentHash = string.Empty,
            BundleJson = "{\"resourceType\":\"Bundle\"}",
            CreatedAt = DateTimeOffset.UtcNow
        };

        contentStoreMock
            .Setup(s => s.StoreAsync(doc.Id, It.IsAny<string>(), doc.BundleJson!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredImportedBundleContent("blob/success.json", 123));

        bundlesMock
            .Setup(b => b.UpdateOneAsync(
                It.IsAny<FilterDefinition<ImportedBundleDocument>>(),
                It.IsAny<UpdateDefinition<ImportedBundleDocument>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        var failedIds = new HashSet<Guid>();
        var result = await service.MigrateInlinePayloadBatchAsync([doc], failedIds, CancellationToken.None);

        Assert.Equal(1, result.Migrated);
        Assert.Equal(0, result.Failed);
        Assert.Empty(failedIds);
        contentStoreMock.Verify(s => s.StoreAsync(doc.Id, It.IsAny<string>(), doc.BundleJson!, It.IsAny<CancellationToken>()), Times.Once);
        bundlesMock.Verify(b => b.UpdateOneAsync(
            It.IsAny<FilterDefinition<ImportedBundleDocument>>(),
            It.IsAny<UpdateDefinition<ImportedBundleDocument>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MigrateInlinePayloadBatchAsync_tracks_failed_documents_and_continues_batch()
    {
        var (service, bundlesMock, contentStoreMock) = CreateService();
        var failedDoc = new ImportedBundleDocument
        {
            Id = Guid.NewGuid(),
            ContentHash = string.Empty,
            BundleJson = "{\"resourceType\":\"Bundle\",\"id\":\"fail\"}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var successDoc = new ImportedBundleDocument
        {
            Id = Guid.NewGuid(),
            ContentHash = string.Empty,
            BundleJson = "{\"resourceType\":\"Bundle\",\"id\":\"ok\"}",
            CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1)
        };

        contentStoreMock
            .Setup(s => s.StoreAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, string, string, CancellationToken>((id, _, json, _) =>
            {
                if (id == failedDoc.Id)
                    throw new InvalidOperationException("store failed");

                return Task.FromResult(new StoredImportedBundleContent("blob/ok.json", json.Length));
            });

        bundlesMock
            .Setup(b => b.UpdateOneAsync(
                It.IsAny<FilterDefinition<ImportedBundleDocument>>(),
                It.IsAny<UpdateDefinition<ImportedBundleDocument>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        var failedIds = new HashSet<Guid>();
        var result = await service.MigrateInlinePayloadBatchAsync([failedDoc, successDoc], failedIds, CancellationToken.None);

        Assert.Equal(1, result.Migrated);
        Assert.Equal(1, result.Failed);
        Assert.Contains(failedDoc.Id, failedIds);
        Assert.DoesNotContain(successDoc.Id, failedIds);
        bundlesMock.Verify(b => b.UpdateOneAsync(
            It.IsAny<FilterDefinition<ImportedBundleDocument>>(),
            It.IsAny<UpdateDefinition<ImportedBundleDocument>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MigrateInlinePayloadBatchAsync_reports_no_progress_when_all_documents_fail()
    {
        var (service, bundlesMock, contentStoreMock) = CreateService();
        var docA = new ImportedBundleDocument { Id = Guid.NewGuid(), BundleJson = "{\"resourceType\":\"Bundle\"}" };
        var docB = new ImportedBundleDocument { Id = Guid.NewGuid(), BundleJson = "{\"resourceType\":\"Bundle\"}" };

        contentStoreMock
            .Setup(s => s.StoreAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("store failed"));

        var failedIds = new HashSet<Guid>();
        var result = await service.MigrateInlinePayloadBatchAsync([docA, docB], failedIds, CancellationToken.None);

        Assert.Equal(0, result.Migrated);
        Assert.Equal(2, result.Failed);
        Assert.Contains(docA.Id, failedIds);
        Assert.Contains(docB.Id, failedIds);
        bundlesMock.Verify(b => b.UpdateOneAsync(
            It.IsAny<FilterDefinition<ImportedBundleDocument>>(),
            It.IsAny<UpdateDefinition<ImportedBundleDocument>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void RemapUploadedBundleIdsInJson_remaps_root_array_with_pascal_case_property()
    {
        var sourceId = Guid.NewGuid();
        var replacementId = Guid.NewGuid();
        var json = $$"""
            [
              {
                "PatientId": "p1",
                "UploadedBundleId": "{{sourceId}}"
              }
            ]
            """;

        var remapped = ImportedBundleBlobMigrationService.RemapUploadedBundleIdsInJson(
            json,
            new Dictionary<Guid, Guid> { [sourceId] = replacementId });

        var root = JsonNode.Parse(remapped) as JsonArray;
        Assert.NotNull(root);
        Assert.Equal(replacementId.ToString(), root![0]?["UploadedBundleId"]?.GetValue<string>());
    }

    [Fact]
    public void RemapUploadedBundleIdsInJson_remaps_object_importedPatientBundles_shape()
    {
        var sourceId = Guid.NewGuid();
        var replacementId = Guid.NewGuid();
        var json = $$"""
            {
              "importedPatientBundles": [
                {
                  "patientId": "p1",
                  "uploadedBundleId": "{{sourceId}}"
                }
              ]
            }
            """;

        var remapped = ImportedBundleBlobMigrationService.RemapUploadedBundleIdsInJson(
            json,
            new Dictionary<Guid, Guid> { [sourceId] = replacementId });

        var root = JsonNode.Parse(remapped) as JsonObject;
        Assert.NotNull(root);
        Assert.Equal(
            replacementId.ToString(),
            root!["importedPatientBundles"]?[0]?["uploadedBundleId"]?.GetValue<string>());
    }

    [Fact]
    public void RemapUploadedBundleIdsInJson_keeps_malformed_json_unchanged()
    {
        const string malformed = "{not valid json";

        var remapped = ImportedBundleBlobMigrationService.RemapUploadedBundleIdsInJson(
            malformed,
            new Dictionary<Guid, Guid>());

        Assert.Equal(malformed, remapped);
    }

    private static (ImportedBundleBlobMigrationService Service, Mock<IMongoCollection<ImportedBundleDocument>> BundlesMock, Mock<IImportedBundleContentStore> ContentStoreMock) CreateService()
    {
        var bundlesMock = new Mock<IMongoCollection<ImportedBundleDocument>>();
        var scenariosMock = new Mock<IMongoCollection<TestScenarioDocument>>();
        var runInputsMock = new Mock<IMongoCollection<AutomationRunInputDocument>>();
        var contentStoreMock = new Mock<IImportedBundleContentStore>();
        var loggerMock = new Mock<ILogger<ImportedBundleBlobMigrationService>>();
        var lifetimeMock = new Mock<IHostApplicationLifetime>();
        var dbMock = new Mock<IMongoDatabase>();

        dbMock.Setup(d => d.GetCollection<ImportedBundleDocument>("automation_imported_bundles", null))
            .Returns(bundlesMock.Object);
        dbMock.Setup(d => d.GetCollection<TestScenarioDocument>("automation_scenarios", null))
            .Returns(scenariosMock.Object);
        dbMock.Setup(d => d.GetCollection<AutomationRunInputDocument>("automation_run_inputs", null))
            .Returns(runInputsMock.Object);

        var service = new ImportedBundleBlobMigrationService(
            dbMock.Object,
            contentStoreMock.Object,
            loggerMock.Object,
            lifetimeMock.Object);

        return (service, bundlesMock, contentStoreMock);
    }
}
