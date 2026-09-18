using Automation.UI.Models;
using Automation.UI.Services.ConfigurationGeneration;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Validation;
using LantanaGroup.Link.Normalization.Application.Operations;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class NormalizationOperationMapperTests
{
    [Fact]
    public void FromDefinition_HslocMap_CreatesHsLocMapOperation()
    {
        var definition = new NormalizationOperationDefinition
        {
            Name = HslocMappingDefaults.OperationName,
            OperationType = "HSLOCMap",
            ResourceTypes = ["Location"],
            CodeMapFhirPath = "type",
            CodeSystemMaps =
            [
                new NormalizationCodeSystemMap
                {
                    SourceSystem = "http://example.org/fhir/sid/location",
                    TargetSystem = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
                    CodeMaps = { ["loc-1"] = new NormalizationCodeMapEntry { Code = "1025-6", Display = "Trauma Critical Care" } }
                }
            ]
        };

        var operation = NormalizationOperationMapper.FromDefinition(definition);

        operation.Should().BeOfType<HSLOCMapOperation>();
        operation!.OperationType.Should().Be(OperationType.HSLOCMap);
        operation.Name.Should().Be(HslocMappingDefaults.OperationName);
        var hsloc = (HSLOCMapOperation)operation;
        hsloc.FhirPath.Should().Be("type");
        hsloc.CodeSystemMaps.Should().ContainSingle(m =>
            m.SourceSystem == "http://example.org/fhir/sid/location"
            && m.CodeMaps.ContainsKey("loc-1")
            && m.CodeMaps["loc-1"].Code == "1025-6");
    }

    [Fact]
    public void FromDefinition_HslocMapWithoutMaps_ReturnsNull()
    {
        var definition = new NormalizationOperationDefinition
        {
            Name = HslocMappingDefaults.OperationName,
            OperationType = "HSLOCMap",
            ResourceTypes = ["Location"]
        };

        NormalizationOperationMapper.FromDefinition(definition).Should().BeNull();
    }
}
