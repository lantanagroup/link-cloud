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
        var race = new Extension { Url = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-race" };
        race.Extension.Add(new Extension("ombCategory", new Coding("urn:oid:2.16.840.1.113883.6.238", "2106-3")));
        race.Extension.Add(new Extension("text", new FhirString("White")));
        patient.Extension.Add(race);
        patient.Extension.Add(new Extension("not-a-url", new FhirString("x")));

        var fp = UploadedBundleAnalyzer.Analyze([patient]);

        fp.Extensions.Should().ContainSingle(e => e.Url.Contains("us-core-race"));
        fp.Extensions.Should().NotContain(e =>
            e.Url == "ombCategory" || e.Url == "text" || e.Url == "not-a-url");
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
        proposal.Notes.Should().Contain(n => n.Contains("before cleanup", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Orm_builder_does_not_reuse_type_only_map_when_upload_has_identifiers_but_no_those_types()
    {
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationIdentifiers = [new LocationIdentifierHint { System = "http://epic.example/locations", Value = "UNIT-1" }]
        };
        var typeOnly = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Epic HSLOC map",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = "Location.type.coding.where(system = 'https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html').exists()"
                }
            ]
        };

        var proposal = OrgResourceMapProposalBuilder.Build(fp, [typeOnly]);
        proposal.Conditions.Should().ContainSingle(c =>
            c.FhirPath == "Location.identifier.where(system = 'http://epic.example/locations').exists()");
        var reuse = proposal.Reuse.Should().ContainSingle(r => r.Id == typeOnly.Id).Subject;
        reuse.Recommendation.Should().Be("Extend");
        reuse.Reason.Should().Contain("before cleanup");
    }

    [Fact]
    public void Orm_builder_reuses_type_only_map_when_those_type_codes_are_already_on_the_upload()
    {
        var hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = "1027-2" }]
        };
        var typeOnly = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "HSLOC map",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.where(system = '{hsloc}' and code = '1027-2').exists()"
                }
            ]
        };

        var proposal = OrgResourceMapProposalBuilder.Build(fp, [typeOnly]);
        proposal.Reuse.Should().ContainSingle(r => r.Recommendation == "Reuse" && r.Id == typeOnly.Id);
    }

    [Fact]
    public void Orm_builder_does_not_treat_value_specific_condition_as_covering_the_whole_system()
    {
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationIdentifiers = [new LocationIdentifierHint { System = "http://a", Value = "UNIT-9" }]
        };
        var hospitalOnly = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Hospital campus only",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = "Location.identifier.where(system = 'http://a' and value = 'HOSP').exists()"
                }
            ]
        };

        var proposal = OrgResourceMapProposalBuilder.Build(fp, [hospitalOnly]);
        proposal.Reuse.Should().NotContain(r => r.Recommendation == "Reuse" && r.Id == hospitalOnly.Id);
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
            "CopyLocationAliasToTypeIteratively",
            "CodeMap",
            "RemoveExtensions"
        ]);
        proposal.Operations.Single(o => o.OperationType == "CopyLocation").ReuseOperationId.Should().Be(existingCopy.Id);
        proposal.Operations.Single(o => o.OperationType == "CopyLocationAliasToTypeIteratively").SplitOnComma.Should().BeTrue();
        proposal.Notes.Should().Contain(n => n.Contains("will not rewrite Encounter.status", StringComparison.OrdinalIgnoreCase));
        proposal.Notes.Should().Contain(n => n.Contains("will not be copied over existing Location.type", StringComparison.OrdinalIgnoreCase));
        proposal.Notes.Should().Contain(n => n.Contains("Org resource maps must match", StringComparison.OrdinalIgnoreCase));
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
        proposal.Operations.Should().NotContain(o => o.OperationType == "CopyProperty");
        proposal.Operations.Should().Contain(o => o.OperationType == "CodeMap");
    }

    [Fact]
    public void Normalization_builder_does_not_rewrite_encounter_status_or_class()
    {
        var fp = new BundleConfigFingerprint
        {
            Codings =
            [
                new CodingHint { ResourceType = "Encounter", Path = "class", System = "http://terminology.hl7.org/CodeSystem/v3-ActCode", Code = "ACUTE" },
                new CodingHint { ResourceType = "Encounter", Path = "class", System = "http://terminology.hl7.org/CodeSystem/v3-ActCode", Code = "IMP" }
            ]
        };

        var proposal = NormalizationProposalBuilder.Build(fp, [], []);
        proposal.Operations.Should().NotContain(o => o.OperationType == "ConditionalTransform");
        proposal.Notes.Should().Contain(n => n.Contains("will not rewrite Encounter.status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Normalization_builder_warns_when_extended_suite_already_writes_eligibility_fields()
    {
        var transformId = Guid.NewGuid();
        var existing = new NormalizationOperationDefinition
        {
            Id = transformId,
            Name = "Set Encounter status when class matches upload",
            OperationType = "ConditionalTransform",
            ResourceTypes = ["Encounter"],
            ConditionTargetFhirPath = "status",
            ConditionTargetValue = "in-progress"
        };
        var suite = new NormalizationSuiteDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Risky suite",
            OperationIds = [transformId]
        };
        var fp = new BundleConfigFingerprint
        {
            Codings =
            [
                new CodingHint { ResourceType = "Encounter", Path = "class", Code = "IMP" }
            ]
        };

        var proposal = NormalizationProposalBuilder.Build(fp, [existing], [suite], [], suite);
        proposal.Notes.Should().Contain(n =>
            n.Contains("eligibility-critical write", StringComparison.OrdinalIgnoreCase)
            && n.Contains("Set Encounter status when class matches upload", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Normalization_builder_seeds_code_map_from_identifiers_when_copy_location_will_run()
    {
        var copyId = Guid.NewGuid();
        var existingCopy = new NormalizationOperationDefinition
        {
            Id = copyId,
            Name = "Copy Location Identifiers to Type",
            OperationType = "CopyLocation",
            ResourceTypes = ["Location"]
        };
        var suite = new NormalizationSuiteDefinition
        {
            Id = Guid.NewGuid(),
            Name = "System Default",
            IsSystem = true,
            SequenceIds = [],
            OperationIds = [copyId]
        };
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationIdentifiers = [new LocationIdentifierHint { System = "http://hospital.example.org/locations", Value = "LOC-1" }],
            Codings =
            [
                new CodingHint
                {
                    ResourceType = "Location",
                    Path = "type.coding",
                    System = "http://terminology.hl7.org/CodeSystem/v3-RoleCode",
                    Code = "HOSP"
                }
            ]
        };

        var proposal = NormalizationProposalBuilder.Build(fp, [existingCopy], [suite], [], suite);
        var codeMap = proposal.Operations.Single(o => o.OperationType == "CodeMap");
        codeMap.CodeSystemMaps.Should().ContainSingle(m =>
            m.SourceSystem == "http://hospital.example.org/locations"
            && m.CodeMaps.ContainsKey("LOC-1"));
    }

    [Fact]
    public void Normalization_builder_does_not_reuse_code_map_with_different_source_system()
    {
        var existingMap = new NormalizationOperationDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Map coding name and code",
            OperationType = "CodeMap",
            ResourceTypes = ["Location"],
            CodeMapFhirPath = "type.coding",
            CodeSystemMaps =
            [
                new NormalizationCodeSystemMap
                {
                    SourceSystem = "urn:oid:1.2.840.114350.1.13.277.3.7.2.686990",
                    TargetSystem = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
                    CodeMaps = { ["1108-0"] = new NormalizationCodeMapEntry { Code = "1108-0", Display = "ED" } }
                }
            ]
        };
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 1,
            Codings =
            [
                new CodingHint
                {
                    ResourceType = "Location",
                    Path = "type.coding",
                    System = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
                    Code = "1109-8",
                    Display = "Pediatric Emergency Department"
                }
            ]
        };

        var proposal = NormalizationProposalBuilder.Build(fp, [existingMap], []);
        var codeMap = proposal.Operations.Single(o => o.OperationType == "CodeMap");
        codeMap.ReuseOperationId.Should().BeNull();
        codeMap.CodeSystemMaps.Should().ContainSingle(m =>
            m.SourceSystem == "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html"
            && m.CodeMaps.ContainsKey("1109-8"));
    }

    [Fact]
    public void Normalization_builder_reuses_code_map_when_source_system_matches()
    {
        var existingId = Guid.NewGuid();
        var hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
        var existingMap = new NormalizationOperationDefinition
        {
            Id = existingId,
            Name = "Map HSLOC type codes",
            OperationType = "CodeMap",
            ResourceTypes = ["Location"],
            CodeMapFhirPath = "type.coding",
            CodeSystemMaps =
            [
                new NormalizationCodeSystemMap
                {
                    SourceSystem = hsloc,
                    TargetSystem = hsloc,
                    CodeMaps = { ["1109-8"] = new NormalizationCodeMapEntry { Code = "1109-8", Display = "Pediatric Emergency Department" } }
                }
            ]
        };
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 1,
            Codings =
            [
                new CodingHint
                {
                    ResourceType = "Location",
                    Path = "type.coding",
                    System = hsloc,
                    Code = "1109-8"
                }
            ]
        };

        var proposal = NormalizationProposalBuilder.Build(fp, [existingMap], []);
        proposal.Operations.Single(o => o.OperationType == "CodeMap").ReuseOperationId.Should().Be(existingId);
    }
}
