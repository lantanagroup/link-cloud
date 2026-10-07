using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using Link.UI.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class FacilityNormalizationRulesTests
{
    private static readonly string[] Catalog = ["Encounter", "Location", "Observation", "Patient"];

    [Fact]
    public void Copy_property_keeps_paths_and_does_not_assign_a_vendor()
    {
        var built = FacilityNormalizationRules.TryBuild(new NormalizationOperationInput
        {
            OperationType = "CopyProperty",
            Name = "Proof copy",
            Description = "Copies an id",
            ResourceTypes = ["Patient"],
            SourceFhirPath = "Patient.id",
            TargetFhirPath = "Patient.identifier"
        }, "hub-1", Catalog, out var request, out var disabled, out var error);

        built.Should().BeTrue(error);
        disabled.Should().BeFalse();
        request!.FacilityId.Should().Be("hub-1");
        request.VendorVersionIds.Should().BeEmpty();
        request.ResourceTypes.Should().Equal("Patient");
        request.Operation.OperationType.Should().Be("CopyProperty");
        request.Operation.SourceFhirPath.Should().Be("Patient.id");
        request.Operation.TargetFhirPath.Should().Be("Patient.identifier");
        request.Operation.CodeSystemMaps.Should().BeNull();
    }

    [Fact]
    public void A_blank_name_is_rejected()
    {
        var built = FacilityNormalizationRules.TryBuild(new NormalizationOperationInput
        {
            OperationType = "CopyProperty",
            Name = " ",
            ResourceTypes = ["Patient"],
            SourceFhirPath = "id",
            TargetFhirPath = "identifier"
        }, "hub-1", Catalog, out _, out _, out var error);

        built.Should().BeFalse();
        error.Should().Be("Name is required.");
    }

    [Fact]
    public void An_unknown_resource_type_is_rejected()
    {
        var built = FacilityNormalizationRules.TryBuild(new NormalizationOperationInput
        {
            OperationType = "CopyProperty",
            Name = "Proof",
            ResourceTypes = ["Slot"],
            SourceFhirPath = "id",
            TargetFhirPath = "identifier"
        }, "hub-1", Catalog, out _, out _, out var error);

        built.Should().BeFalse();
        error.Should().Contain("Slot");
    }

    [Fact]
    public void Copy_location_adds_location_when_the_form_omits_it()
    {
        var built = FacilityNormalizationRules.TryBuild(new NormalizationOperationInput
        {
            OperationType = "copylocation",
            Name = "Copy locations",
            ResourceTypes = ["Patient"]
        }, "hub-1", Catalog, out var request, out _, out var error);

        built.Should().BeTrue(error);
        request!.ResourceTypes.Should().Equal("Patient", "Location");
        request.Operation.OperationType.Should().Be("CopyLocation");
    }

    [Fact]
    public void Hsloc_map_uses_location_and_the_nhsn_system()
    {
        var built = FacilityNormalizationRules.TryBuild(new NormalizationOperationInput
        {
            OperationType = "HSLOCMap",
            Maps =
            [
                new CodeSystemMapInput
                {
                    SourceSystem = "http://example.org/local",
                    TargetSystem = "http://example.org/other",
                    Entries =
                    [
                        new CodeMapEntryInput { SourceCode = "ward", Code = "1026-8", Display = "Medical ward" },
                        new CodeMapEntryInput()
                    ]
                },
                new CodeSystemMapInput { TargetSystem = FacilityNormalizationRules.HslocSystem, Entries = [new CodeMapEntryInput()] }
            ]
        }, "hub-1", Catalog, out var request, out _, out var error);

        built.Should().BeTrue(error);
        request!.ResourceTypes.Should().Equal("Location");
        request.Operation.Name.Should().Be(FacilityNormalizationRules.HslocName);
        request.Operation.FhirPath.Should().Be("type");
        request.Operation.CodeSystemMaps.Should().ContainSingle();
        request.Operation.CodeSystemMaps![0].TargetSystem.Should().Be(FacilityNormalizationRules.HslocSystem);
        request.Operation.CodeSystemMaps[0].CodeMaps.Should().ContainKey("ward");
    }

    [Fact]
    public void A_repeated_source_code_is_rejected()
    {
        var built = FacilityNormalizationRules.TryBuild(new NormalizationOperationInput
        {
            OperationType = "CodeMap",
            Name = "Map codes",
            FhirPath = "type",
            ResourceTypes = ["Observation"],
            Maps =
            [
                new CodeSystemMapInput
                {
                    SourceSystem = "http://example.org/local",
                    TargetSystem = "http://example.org/target",
                    Entries =
                    [
                        new CodeMapEntryInput { SourceCode = "a", Code = "1", Display = "One" },
                        new CodeMapEntryInput { SourceCode = "a", Code = "2", Display = "Two" }
                    ]
                }
            ]
        }, "hub-1", Catalog, out _, out _, out var error);

        built.Should().BeFalse();
        error.Should().Contain("repeated");
    }

    [Fact]
    public void Conditional_transform_drops_a_blank_row_and_keeps_exists_without_a_value()
    {
        var built = FacilityNormalizationRules.TryBuild(new NormalizationOperationInput
        {
            OperationType = "ConditionalTransform",
            Name = "Set status",
            ResourceTypes = ["Encounter"],
            TargetFhirPath = "status",
            TargetValue = "finished",
            Conditions =
            [
                new ConditionInput { FhirPathSource = "status", Operator = 0, Value = "in-progress" },
                new ConditionInput { FhirPathSource = "period.end", Operator = 6 },
                new ConditionInput()
            ]
        }, "hub-1", Catalog, out var request, out _, out var error);

        built.Should().BeTrue(error);
        request!.Operation.Conditions.Should().HaveCount(2);
        request.Operation.Conditions![1].Operator.Should().Be(6);
        request.Operation.Conditions[1].Value.Should().BeNull();
    }

    [Fact]
    public void Equal_requires_a_value()
    {
        var built = FacilityNormalizationRules.TryBuild(new NormalizationOperationInput
        {
            OperationType = "ConditionalTransform",
            Name = "Set status",
            ResourceTypes = ["Encounter"],
            TargetFhirPath = "status",
            TargetValue = "finished",
            Conditions = [new ConditionInput { FhirPathSource = "status", Operator = 0 }]
        }, "hub-1", Catalog, out _, out _, out var error);

        built.Should().BeFalse();
        error.Should().Contain("value");
    }

    [Fact]
    public void Extension_urls_reject_a_repeat_and_ignore_a_blank_row()
    {
        var built = FacilityNormalizationRules.TryBuild(new NormalizationOperationInput
        {
            OperationType = "RemoveExtensions",
            Name = "Drop extensions",
            ResourceTypes = ["Patient"],
            ExtensionUrls =
            [
                new ExtensionUrlInput { Url = "http://example.org/ext" },
                new ExtensionUrlInput { Url = "http://example.org/ext" }
            ]
        }, "hub-1", Catalog, out _, out _, out var error);

        built.Should().BeFalse();
        error.Should().Contain("repeated");

        built = FacilityNormalizationRules.TryBuild(new NormalizationOperationInput
        {
            OperationType = "RemoveExtensions",
            Name = "Drop extensions",
            ResourceTypes = ["Patient"],
            ExtensionUrls =
            [
                new ExtensionUrlInput { Url = "http://example.org/ext" },
                new ExtensionUrlInput { Url = " ", Remove = true },
                new ExtensionUrlInput()
            ]
        }, "hub-1", Catalog, out var request, out _, out error);

        built.Should().BeTrue(error);
        request!.Operation.ExtensionUrls.Should().Equal("http://example.org/ext");
    }

    [Fact]
    public void Alias_iterations_must_be_from_1_to_1000()
    {
        var built = FacilityNormalizationRules.TryBuild(new NormalizationOperationInput
        {
            OperationType = "CopyLocationAliasToTypeIteratively",
            Name = "Aliases",
            MaxIterations = 0,
            SplitOnComma = true
        }, "hub-1", Catalog, out _, out _, out var error);

        built.Should().BeFalse();
        error.Should().Contain("1 to 1000");

        built = FacilityNormalizationRules.TryBuild(new NormalizationOperationInput
        {
            OperationType = "CopyLocationAliasToTypeIteratively",
            Name = "Aliases",
            MaxIterations = 15,
            SplitOnComma = true
        }, "hub-1", Catalog, out var request, out _, out error);

        built.Should().BeTrue(error);
        request!.ResourceTypes.Should().Equal("Location");
        request.Operation.MaxIterations.Should().Be(15);
        request.Operation.SplitOnComma.Should().BeTrue();
    }

    [Fact]
    public void A_blank_sequence_row_is_left_out_and_an_empty_sequence_clears()
    {
        var id = Guid.NewGuid();
        var other = Guid.NewGuid();
        var built = FacilityNormalizationRules.TryBuildSequence("patient", Catalog,
        [
            new NormalizationSequenceEntryInput { OperationId = id, Sequence = 2 },
            new NormalizationSequenceEntryInput { OperationId = other }
        ], out var sequences, out var clear, out var error);

        built.Should().BeTrue(error);
        clear.Should().BeFalse();
        sequences.Should().ContainSingle();
        sequences[0].OperationId.Should().Be(id);
        sequences[0].Sequence.Should().Be(2);

        built = FacilityNormalizationRules.TryBuildSequence("Patient", Catalog,
        [
            new NormalizationSequenceEntryInput { OperationId = id }
        ], out sequences, out clear, out error);

        built.Should().BeTrue(error);
        clear.Should().BeTrue();
        sequences.Should().BeEmpty();
    }

    [Fact]
    public void Repeated_sequence_numbers_are_rejected()
    {
        var built = FacilityNormalizationRules.TryBuildSequence("Patient", Catalog,
        [
            new NormalizationSequenceEntryInput { OperationId = Guid.NewGuid(), Sequence = 1 },
            new NormalizationSequenceEntryInput { OperationId = Guid.NewGuid(), Sequence = 1 }
        ], out _, out _, out var error);

        built.Should().BeFalse();
        error.Should().Contain("unique");
    }

    [Fact]
    public void Stored_json_round_trips_pascal_and_camel_case()
    {
        var pascal = FacilityNormalizationRules.FromOperation(new NormalizationOperationApiModel
        {
            Id = Guid.NewGuid(),
            OperationType = "CopyProperty",
            Name = "Stored",
            OperationJson = """{"OperationType":"CopyProperty","Name":"From json","Description":"desc","SourceFhirPath":"id","TargetFhirPath":"identifier"}""",
            OperationResourceTypes =
            [
                new NormalizationOperationResourceTypeApiModel
                {
                    Resource = new NormalizationResourceApiModel { ResourceName = "Patient" }
                }
            ]
        });

        pascal.ParseFailed.Should().BeFalse();
        pascal.Name.Should().Be("From json");
        pascal.SourceFhirPath.Should().Be("id");
        pascal.ResourceTypes.Should().Equal("Patient");

        var camel = FacilityNormalizationRules.FromOperation(new NormalizationOperationApiModel
        {
            Id = Guid.NewGuid(),
            OperationType = "ConditionalTransform",
            OperationJson = """{"operationType":"ConditionalTransform","name":"Status","targetFhirPath":"status","targetValue":"finished","conditions":[{"fhirPathSource":"status","operator":"NotExists"}]}"""
        });

        camel.ParseFailed.Should().BeFalse();
        camel.TargetFhirPath.Should().Be("status");
        camel.Conditions.Should().HaveCount(2);
        camel.Conditions![0].Operator.Should().Be(7);
    }

    [Fact]
    public void A_test_resource_must_be_json_with_resource_type()
    {
        FacilityNormalizationRules.TryTestResource("not json", out _, out var error).Should().BeFalse();
        error.Should().Contain("JSON");

        FacilityNormalizationRules.TryTestResource("""{"id":"1"}""", out _, out error).Should().BeFalse();
        error.Should().Contain("resourceType");

        FacilityNormalizationRules.TryTestResource("""{"resourceType":"Patient","id":"1"}""", out var json, out error).Should().BeTrue(error);
        json.Should().Contain("Patient");
    }
}
