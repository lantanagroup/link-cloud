using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class NormalizationRuntimeSequencePlannerTests
{
    [Fact]
    public void Plan_flattens_extended_suite_into_per_resource_type_runtime_numbers()
    {
        var copyLocation = Op("Copy Location Identifiers to Type", "CopyLocation", "Location");
        var copyProperty = Op("Copy Location Identifier Value to Type Code", "CopyProperty", "Location");
        var removeCommon = Op("Remove Common Extensions", "RemoveExtensions", "Encounter", "Patient");
        var copyAlias = Op("Copy Location aliases to type iteratively", "CopyLocationAliasToTypeIteratively", "Location");
        var transform = Op("Set Encounter status when class matches upload", "ConditionalTransform", "Encounter");
        var codeMap = Op("Code map Location.type", "CodeMap", "Location");

        var suite = new NormalizationSuiteResolution(
            "System Default extended",
            [copyLocation, copyProperty, removeCommon, copyAlias, transform, codeMap],
            [
                new NormalizationSuiteSequenceResolution(
                    "Default Location Normalization",
                    [
                        new NormalizationSuiteSequenceOperationResolution(1, copyLocation),
                        new NormalizationSuiteSequenceOperationResolution(2, copyProperty)
                    ]),
                new NormalizationSuiteSequenceResolution(
                    "Default Cleanup",
                    [new NormalizationSuiteSequenceOperationResolution(1, removeCommon)]),
                new NormalizationSuiteSequenceResolution(
                    "Generated patient sequence",
                    [
                        new NormalizationSuiteSequenceOperationResolution(1, copyAlias),
                        new NormalizationSuiteSequenceOperationResolution(2, transform),
                        new NormalizationSuiteSequenceOperationResolution(3, codeMap)
                    ])
            ],
            []);

        var planned = NormalizationRuntimeSequencePlanner.Plan(suite);

        planned.Should().ContainSingle(s =>
            s.Operation.Name == copyAlias.Name
            && s.SuiteSequence == 1
            && s.RuntimeSequence == 3
            && s.ResourceType == "Location");

        planned.Should().ContainSingle(s =>
            s.Operation.Name == transform.Name
            && s.SuiteSequence == 2
            && s.RuntimeSequence == 2
            && s.ResourceType == "Encounter");

        planned.Should().ContainSingle(s =>
            s.Operation.Name == codeMap.Name
            && s.SuiteSequence == 3
            && s.RuntimeSequence == 4
            && s.ResourceType == "Location");

        planned.Single(s => s.Operation.Name == removeCommon.Name && s.ResourceType == "Encounter")
            .RuntimeSequence.Should().Be(1);
        planned.Single(s => s.Operation.Name == removeCommon.Name && s.ResourceType == "Patient")
            .RuntimeSequence.Should().Be(1);
    }

    private static NormalizationOperationDefinition Op(string name, string type, params string[] resourceTypes) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        OperationType = type,
        ResourceTypes = [.. resourceTypes]
    };
}
