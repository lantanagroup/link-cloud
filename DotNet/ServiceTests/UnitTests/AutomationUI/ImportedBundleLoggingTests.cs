using System.Text;
using Automation.UI.Controllers;
using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.ServiceTests.UnitTests.AutomationUI;

public class ImportedBundleLoggingTests
{
    [Fact]
    public async Task UploadKeepsOriginalContentButCleansLogValues()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        const string patientId = "patient\r\nforged";
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            resourceType = "Bundle", type = "collection",
            entry = new[] { new { resource = new { resourceType = "Patient", id = patientId } } }
        });
        var collection = new Mock<IMongoCollection<ImportedBundleDocument>>();
        var doc = new ImportedBundleDocument { Id = Guid.NewGuid(), ContentHash = "hash" };
        collection.Setup(c => c.FindOneAndUpdateAsync(
                It.IsAny<FilterDefinition<ImportedBundleDocument>>(), It.IsAny<UpdateDefinition<ImportedBundleDocument>>(),
                It.IsAny<FindOneAndUpdateOptions<ImportedBundleDocument, ImportedBundleDocument>>(), token))
            .ReturnsAsync(doc);
        collection.Setup(c => c.UpdateOneAsync(It.IsAny<FilterDefinition<ImportedBundleDocument>>(),
                It.IsAny<UpdateDefinition<ImportedBundleDocument>>(), It.IsAny<UpdateOptions>(), token))
            .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));
        var database = new Mock<IMongoDatabase>();
        database.Setup(d => d.GetCollection<ImportedBundleDocument>("automation_imported_bundles", null)).Returns(collection.Object);
        var content = new Mock<IImportedBundleContentStore>();
        content.Setup(s => s.StoreAsync(doc.Id, doc.ContentHash, json, token))
            .ReturnsAsync(new StoredImportedBundleContent("blob\r\nforged", json.Length));
        var logger = new Mock<ILogger<ScenariosController>>();
        using var replacement = new PatientReplacementManager(Options.Create(new AutomationConfig()), NullLogger<PatientReplacementManager>.Instance);
        var controller = new ScenariosController(Mock.Of<IScenarioStore>(), Mock.Of<IQueryPlanTemplateStore>(),
            Mock.Of<INormalizationStore>(), Mock.Of<IOrganizationResourceMapTemplateStore>(), Mock.Of<IPatientConfigurationStore>(),
            Mock.Of<IMeasureTemplateStore>(), Mock.Of<IFacilityTemplateStore>(),
            Options.Create(new AutomationConfig { FhirServerBase = "http://127.0.0.1:1/fhir" }),
            database.Object, content.Object, replacement, logger.Object);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await controller.UploadImportedBundle(new ScenariosController.UploadImportedBundleRequest
        {
            File = new FormFile(stream, 0, stream.Length, "file", "bundle\r\nforged.json")
        }, token);

        Assert.IsType<JsonResult>(result);
        content.Verify(s => s.StoreAsync(doc.Id, doc.ContentHash, json, token), Times.Once);
        var messages = logger.Invocations.Where(i => i.Method.Name == "Log").Select(i => i.Arguments[2].ToString()!).ToList();
        Assert.Contains(messages, m => m.StartsWith("Stored imported bundle"));
        Assert.All(messages, message => Assert.DoesNotContain(message, char.IsControl));
    }
}
