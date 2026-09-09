using System.Text.Json;
using System.Text.Json.Serialization;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Engine;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class HSLOCMapOperationTests
{
    [Fact]
    public void OperationType_IsPreservedThroughBaseAndInterface()
    {
        CodeMapOperation operation = new HSLOCMapOperation("HSLOC map", "type.coding", [], "Description");

        Assert.Equal(OperationType.HSLOCMap, operation.OperationType);
        Assert.Equal(OperationType.HSLOCMap, ((IOperation)operation).OperationType);
        Assert.Equal(OperationType.CodeMap, new CodeMapOperation("Code map", "type.coding", []).OperationType);
    }

    [Fact]
    public void PersistedOperation_RoundTripsAsHSLOCMap()
    {
        var operation = new HSLOCMapOperation("HSLOC map", "type.coding",
            [new CodeSystemMap("urn:local", "urn:hsloc", new Dictionary<string, CodeMap>
            {
                ["ICU"] = new CodeMap("1027-4", "Medical critical care")
            })], "Description");
        var json = JsonSerializer.Serialize(operation);

        var restored = Assert.IsType<HSLOCMapOperation>(OperationHelper.GetOperation("HSLOCMap", json));

        Assert.Equal(operation.Name, restored.Name);
        Assert.Equal(operation.Description, restored.Description);
        Assert.Equal(operation.FhirPath, restored.FhirPath);
        var map = Assert.Single(restored.CodeSystemMaps);
        Assert.Equal("urn:local", map.SourceSystem);
        Assert.Equal("urn:hsloc", map.TargetSystem);
        Assert.Equal("1027-4", map.CodeMaps["ICU"].Code);
        Assert.Equal("Medical critical care", map.CodeMaps["ICU"].Display);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonConverter_RoundTripsAsHSLOCMap(bool camelCase)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = camelCase ? JsonNamingPolicy.CamelCase : null,
            Converters = { new OperationConverter(), new JsonStringEnumConverter() }
        };
        IOperation operation = new HSLOCMapOperation("HSLOC map", "type.coding", [], "Description");

        var json = JsonSerializer.Serialize(operation, options);
        var restored = Assert.IsType<HSLOCMapOperation>(JsonSerializer.Deserialize<IOperation>(json, options));

        Assert.Equal(OperationType.HSLOCMap, restored.OperationType);
        Assert.Equal(operation.Name, restored.Name);
        Assert.Equal("type.coding", restored.FhirPath);
        Assert.NotNull(restored.CodeSystemMaps);
    }

    [Fact]
    public async Task Placeholder_ResolvesFromEngineRegistrationAndDoesNotModifyResource()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNormalizationEngine();
        using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<NormalizationEngine>();
        var operation = new HSLOCMapOperation("HSLOC map", "type.coding", []);
        var resource = new Hl7.Fhir.Model.Location { Id = "location" };
        var original = (Hl7.Fhir.Model.Location)resource.DeepCopy();

        var outcomes = await engine.ApplyAsync(resource, [new NormalizationWorkItem(1, operation, ["Location"])]);
        var result = Assert.Single(outcomes).Result;

        Assert.Same(operation, OperationServiceHelper.GetOperationImplementation(operation));
        Assert.Equal(OperationStatus.Failure, result.SuccessCode);
        Assert.Contains("not implemented", result.ErrorMessage);
        Assert.Same(resource, result.Resource);
        Assert.True(resource.IsExactly(original));
        Assert.Null(result.CodeMapping);
    }

    [Fact]
    public void InvalidFhirPath_UsesBaseValidation()
    {
        Assert.Throws<ArgumentException>(() => new HSLOCMapOperation("HSLOC map", "(", []));
    }
}