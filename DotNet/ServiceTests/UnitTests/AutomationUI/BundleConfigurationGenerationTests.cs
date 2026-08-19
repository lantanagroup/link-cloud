using Automation.UI.Models;
using Automation.UI.Services.ConfigurationGeneration;
using FluentAssertions;
using Hl7.Fhir.Model;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class BundleConfigurationGenerationTests
{
    [Fact]
    public void Analyzer_ignores_relative_nested_extension_urls()
    {
        var patient = new Patient { Id = "p1" };
        var race = new Extension("http://hl7.org/fhir/us/core/StructureDefinition/us-core-race");
        race.Extension.Add(new Extension("ombCategory", new Coding("urn:oid:2.16.840.1.113883.6.238", "2106-3")));
        race.Extension.Add(new Extension("text", new FhirString("White")));
        patient.Extension.Add(race);
        patient.Extension.Add(new Extension("not-a-url", new FhirString("x")));

        var fp = UploadedBundleAnalyzer.Analyze([patient]);

        fp.Extensions.Should().ContainSingle(e => e.Url.Contains("us-core-race"));
        fp.Extensions.Should().NotContain(e => e.Url is "ombCategory" or "text" or "not-a-url");
    }

    [Fact]
    public void Analyzer_extracts_location_identifiers_types_aliases_and_extensions()
    {
        var location = new Location
        {
            Id = "loc-1",
            Identifier =
            [
                new Identifier("http://example.org/fhir/sid/location", "HOSP-1")
            ],
            Type =
            [
                new CodeableConcept("https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html", "1027-2", "Medical Ward")
            ],
            Alias = ["Main Hospital, Campus A"]
        };
        location.Extension.Add(new Extension("http://open.epic.com/FHIR/StructureDefinition/extension/epic-id", new FhirString("x")));

        var patient = new Patient { Id = "p1" };
        patient.Extension.Add(new Extension("https://open.epic.com/FHIR/StructureDefinition/extension/patient-merge-unmerge-instant", new FhirString("t")));

        var fp = UploadedBundleAnalyzer.Analyze([patient, location]);

        fp.PatientCount.Should().Be(1);
        fp.LocationCount.Should().Be(1);
        fp.LocationsWithoutIdentifier.Should().Be(0);
        fp.LocationIdentifiers.Should().ContainSingle(i => i.System.Contains("location") && i.Value == "HOSP-1");
        fp.LocationTypes.Should().ContainSingle(t => t.Code == "1027-2");
        fp.LocationAliases.Should().Contain("Main Hospital, Campus A");
        fp.Extensions.Should().Contain(e => e.Url.Contains("epic-id") && e.ResourceType == "Location");
        fp.Extensions.Should().Contain(e => e.Url.Contains("patient-merge") && e.ResourceType == "Patient");
    }

    [Fact]
    public void Analyzer_merge_unions_fingerprints_from_multiple_patients()
    {
        var first = UploadedBundleAnalyzer.Analyze([
            new Location
            {
                Identifier = [new Identifier("http://a", "1")],
                Type = [new CodeableConcept("http://t", "A", "A")]
            }
        ]);
        var second = UploadedBundleAnalyzer.Analyze([
            new Location
            {
                Identifier = [new Identifier("http://b", "2")],
                Type = [new CodeableConcept("http://t", "A", "A")]
            }
        ]);

        var merged = UploadedBundleAnalyzer.Merge(first, second);
        merged.LocationIdentifiers.Should().HaveCount(2);
        merged.LocationTypes.Should().ContainSingle();
        merged.LocationCount.Should().Be(2);
    }

    [Fact]
    public void Analyzer_counts_locations_without_identifiers()
    {
        var fp = UploadedBundleAnalyzer.Analyze([
            new Location { Type = [new CodeableConcept("http://t", "A", "A")] }
        ]);
        fp.LocationsWithoutIdentifier.Should().Be(1);
    }

    [Fact]
    public void Orm_builder_creates_system_level_conditions_and_recommends_reuse()
    {
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationIdentifiers = [new LocationIdentifierHint { System = "http://a", Value = "1" }],
            LocationTypes = [new LocationTypeHint { System = "http://t", Code = "A" }]
        };

        var existing = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Existing A",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = "Location.identifier.exists(system = 'http://a' and value = '1')"
                },
                new OrganizationResourceMapCondition
                {
                    FhirPath = "Location.type.coding.exists(system = 'http://t' and code = 'A')"
                }
            ]
        };

        var proposal = OrgResourceMapProposalBuilder.Build(fp, [existing]);
        proposal.Conditions.Should().ContainSingle();
        proposal.Conditions[0].FhirPath.Should().Be("Location.identifier.where(system = 'http://a').exists()");
        proposal.Reuse.Should().ContainSingle(r => r.Recommendation == "Reuse" && r.Id == existing.Id);
    }

    [Fact]
    public void Orm_builder_recognizes_system_default_or_style_path()
    {
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationIdentifiers = [new LocationIdentifierHint { System = "http://example.org/fhir/sid/location", Value = "HOSP-1" }]
        };

        var systemDefault = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "System Default",
            IsSystem = true,
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = "identifier.where(system='http://example.org/fhir/sid/location').exists() or type.coding.where(system='https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html').exists()"
                }
            ]
        };

        var proposal = OrgResourceMapProposalBuilder.Build(fp, [systemDefault]);
        proposal.Reuse.Should().ContainSingle(r => r.Recommendation == "Reuse" && r.Id == systemDefault.Id);
    }

    [Fact]
    public void Orm_builder_adds_type_conditions_when_locations_lack_identifiers()
    {
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationsWithoutIdentifier = 1,
            LocationTypes = [new LocationTypeHint { System = "http://t", Code = "A" }]
        };

        var proposal = OrgResourceMapProposalBuilder.Build(fp, []);
        proposal.Conditions.Should().ContainSingle(c =>
            c.FhirPath == "Location.type.coding.where(system = 'http://t' and code = 'A').exists()");
    }

    [Fact]
    public void Orm_builder_extends_existing_map_with_new_identifier_systems()
    {
        var existing = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Partial",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = "Location.identifier.where(system = 'http://a').exists()"
                }
            ]
        };
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 2,
            LocationIdentifiers =
            [
                new LocationIdentifierHint { System = "http://a", Value = "1" },
                new LocationIdentifierHint { System = "http://b", Value = "2" }
            ]
        };

        var proposal = OrgResourceMapProposalBuilder.Build(fp, [existing], existing);
        proposal.Conditions.Should().HaveCount(2);
        proposal.Conditions.Select(c => c.FhirPath).Should().Contain("Location.identifier.where(system = 'http://b').exists()");
        proposal.Reuse.Should().ContainSingle(r => r.Recommendation == "Extend");
    }

    [Fact]
    public void Normalization_builder_emits_one_of_each_supported_type_when_data_allows()
    {
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationIdentifiers = [new LocationIdentifierHint { System = "http://a", Value = "1" }],
            LocationAliases = ["Ward, East"],
            LocationTypes = [new LocationTypeHint { System = "http://t", Code = "A" }],
            Codings =
            [
                new CodingHint { ResourceType = "Location", Path = "type.coding", System = "http://t", Code = "A", Display = "A" },
                new CodingHint { ResourceType = "Encounter", Path = "class", System = "http://terminology.hl7.org/CodeSystem/v3-ActCode", Code = "IMP", Display = "inpatient encounter" }
            ],
            Extensions =
            [
                new ExtensionHint { ResourceType = "Encounter", Url = "http://open.epic.com/FHIR/StructureDefinition/extension/epic-id" }
            ]
        };

        var existingCopy = new NormalizationOperationDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Existing CopyLocation",
            OperationType = "CopyLocation",
            ResourceTypes = ["Location"]
        };

        var proposal = NormalizationProposalBuilder.Build(fp, [existingCopy], []);
        proposal.Operations.Select(o => o.OperationType).Should().BeEquivalentTo([
            "CopyLocation",
            "CopyProperty",
            "CopyLocationAliasToTypeIteratively",
            "ConditionalTransform",
            "CodeMap",
            "RemoveExtensions"
        ]);
        proposal.Operations.Single(o => o.OperationType == "CopyLocation").ReuseOperationId.Should().Be(existingCopy.Id);
        proposal.Operations.Single(o => o.OperationType == "CopyLocationAliasToTypeIteratively").SplitOnComma.Should().BeTrue();
        var transform = proposal.Operations.Single(o => o.OperationType == "ConditionalTransform");
        transform.ConditionTargetFhirPath.Should().Be("status");
        transform.ConditionTargetValue.Should().Be("in-progress");
        transform.ResourceTypes.Should().Equal("Encounter");
    }

    [Fact]
    public void Normalization_builder_does_not_invent_location_text_conditional_transform()
    {
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationIdentifiers = [new LocationIdentifierHint { System = "http://a", Value = "1" }]
        };

        var proposal = NormalizationProposalBuilder.Build(fp, [], []);
        proposal.Operations.Should().NotContain(o => o.OperationType == "ConditionalTransform");
        proposal.Operations.Should().NotContain(o =>
            string.Equals(Convert.ToString(o.ConditionTargetValue), "Organization Location", StringComparison.Ordinal));
    }

    [Fact]
    public void Normalization_builder_skips_types_already_in_suite_being_refined()
    {
        var copyId = Guid.NewGuid();
        var existingCopy = new NormalizationOperationDefinition
        {
            Id = copyId,
            Name = "Existing CopyLocation",
            OperationType = "CopyLocation",
            ResourceTypes = ["Location"]
        };
        var suite = new NormalizationSuiteDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Partial suite",
            OperationIds = [copyId]
        };
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationIdentifiers = [new LocationIdentifierHint { System = "http://a", Value = "1" }]
        };

        var proposal = NormalizationProposalBuilder.Build(fp, [existingCopy], [suite], [], suite);
        proposal.Operations.Should().NotContain(o => o.OperationType == "CopyLocation");
        proposal.Operations.Should().Contain(o => o.OperationType == "CopyProperty");
    }
}
