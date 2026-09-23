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
        fp.RawLocations.Should().ContainSingle();
        fp.RawLocations[0].Aliases.Should().ContainSingle("Main Hospital, Campus A");
    }

    [Fact]
    public void Analyzer_keeps_one_signature_per_distinct_type_and_alias_pair()
    {
        Location Make(string code, string alias) => new()
        {
            Type = [new CodeableConcept("http://t", code, code)],
            Alias = [alias]
        };

        var fp = UploadedBundleAnalyzer.Analyze([
            Make("1099-1", "ICU"),
            Make("1099-1", "ICU"),
            Make("1099-1", "Ward")
        ]);

        fp.LocationCount.Should().Be(3);
        fp.RawLocations.Should().HaveCount(2);
        fp.RawLocations.Should().Contain(location =>
            location.Aliases.Contains("ICU") && location.Types.Any(type => type.Code == "1099-1"));
        fp.RawLocations.Should().Contain(location => location.Aliases.Contains("Ward"));
    }

    [Fact]
    public void Analyzer_keeps_type_codes_that_differ_only_by_case()
    {
        var location = new Location
        {
            Type =
            [
                new CodeableConcept("http://t", "ABC", "ABC"),
                new CodeableConcept("http://t", "abc", "abc")
            ]
        };

        var fp = UploadedBundleAnalyzer.Analyze([location]);

        fp.LocationTypes.Select(type => type.Code).Should().BeEquivalentTo("ABC", "abc");
        fp.RawLocations.Should().ContainSingle();
        fp.RawLocations[0].Types.Select(type => type.Code).Should().BeEquivalentTo("ABC", "abc");
    }

    [Fact]
    public void Analyzer_keeps_identifier_values_that_differ_only_by_case()
    {
        var location = new Location
        {
            Identifier = [new Identifier("http://a", "HOSP"), new Identifier("http://a", "hosp")]
        };

        var fp = UploadedBundleAnalyzer.Analyze([location]);

        fp.LocationIdentifiers.Select(identifier => identifier.Value).Should().BeEquivalentTo("HOSP", "hosp");
    }

    [Fact]
    public void Analyzer_keeps_exact_location_alias_on_the_raw_location()
    {
        var location = new Location
        {
            Type = [new CodeableConcept("http://t", "1099-1", "1099-1")],
            Alias = [" ICU "]
        };

        var fp = UploadedBundleAnalyzer.Analyze([location]);

        fp.RawLocations.Should().ContainSingle();
        fp.RawLocations[0].Aliases.Should().ContainSingle(" ICU ");
        fp.LocationAliases.Should().ContainSingle("ICU");
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
    public void Analyzer_merge_keeps_type_codes_that_differ_only_by_case()
    {
        const string system = "http://t";
        var upper = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = system, Code = "ABC" }]
        };
        var lower = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = system, Code = "abc" }]
        };
        var merged = UploadedBundleAnalyzer.Merge(upper, lower);
        merged.LocationTypes.Select(type => type.Code).Should().BeEquivalentTo("ABC", "abc");

        var map = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Lower abc",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.exists(system = '{system}' and code = 'abc')"
                }
            ]
        };
        OrgResourceMapProposalBuilder.Build(merged, [map]).Reuse
            .Should().ContainSingle(r => r.Id == map.Id && r.Recommendation == "Reuse");
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

        var typeOnlyUpload = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationTypes =
            [
                new LocationTypeHint
                {
                    System = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
                    Code = "1099-1"
                }
            ]
        };
        OrgResourceMapProposalBuilder.Build(typeOnlyUpload, [systemDefault]).Reuse
            .Should().ContainSingle(r => r.Recommendation == "Reuse" && r.Id == systemDefault.Id);
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
    public void Orm_builder_reuses_type_only_map_when_required_code_is_present_among_other_raw_types()
    {
        const string hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
        const string role = "http://terminology.hl7.org/CodeSystem/v3-RoleCode";
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 2,
            LocationIdentifiers = [new LocationIdentifierHint { System = role, Value = "HU" }],
            LocationTypes =
            [
                new LocationTypeHint { System = hsloc, Code = "1099-1" },
                new LocationTypeHint { System = hsloc, Code = "1027-2" },
                new LocationTypeHint { System = hsloc, Code = "1052-0" },
                new LocationTypeHint { System = role, Code = "HU" },
                new LocationTypeHint { System = hsloc, Code = "1039-7" },
                new LocationTypeHint { System = hsloc, Code = "1060-3" },
                new LocationTypeHint { System = hsloc, Code = "1026-4" },
                new LocationTypeHint { System = hsloc, Code = "1198-3" },
                new LocationTypeHint { System = hsloc, Code = "1205-6" }
            ]
        };
        var typeOnly = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Step down 1099-1",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.exists(system = '{hsloc}' and code = '1099-1')"
                }
            ]
        };

        var proposal = OrgResourceMapProposalBuilder.Build(fp, [typeOnly]);
        var reuse = proposal.Reuse.Should().ContainSingle(r => r.Id == typeOnly.Id).Subject;
        reuse.Recommendation.Should().Be("Reuse");
        reuse.Score.Should().Be(0.11);
        reuse.Reason.Should().Contain("already present");

        var wider = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Any HSLOC",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.where(system = '{hsloc}').exists()"
                }
            ]
        };
        var ranked = OrgResourceMapProposalBuilder.Build(fp, [typeOnly, wider]).Reuse;
        ranked[0].Id.Should().Be(wider.Id);
        ranked[0].Recommendation.Should().Be("Reuse");
        ranked[0].Score.Should().Be(0.89);
        ranked.Should().Contain(r => r.Id == typeOnly.Id && r.Recommendation == "Reuse" && r.Score == 0.11);
    }

    [Fact]
    public void Orm_builder_does_not_reuse_type_only_map_when_only_a_different_code_on_that_system_is_uploaded()
    {
        const string hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationIdentifiers = [new LocationIdentifierHint { System = "http://a", Value = "UNIT-9" }],
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = "1027-2" }]
        };
        var typeOnly = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Step down 1099-1",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.exists(system = '{hsloc}' and code = '1099-1')"
                }
            ]
        };

        var proposal = OrgResourceMapProposalBuilder.Build(fp, [typeOnly]);
        proposal.Reuse.Should().NotContain(r => r.Recommendation == "Reuse" && r.Id == typeOnly.Id);
    }

    [Fact]
    public void Orm_builder_reuses_type_only_map_when_any_of_its_codes_is_on_the_upload()
    {
        const string hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
        var map = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Two HSLOC codes",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.where(system = '{hsloc}' and code = '1099-1').exists()"
                },
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.where(system = '{hsloc}' and code = '1027-2').exists()"
                }
            ]
        };

        var partial = new BundleConfigFingerprint
        {
            LocationCount = 2,
            LocationTypes =
            [
                new LocationTypeHint { System = hsloc, Code = "1099-1" },
                new LocationTypeHint { System = hsloc, Code = "1052-0" },
                new LocationTypeHint { System = "http://terminology.hl7.org/CodeSystem/v3-RoleCode", Code = "HU" }
            ]
        };
        var partialReuse = OrgResourceMapProposalBuilder.Build(partial, [map]).Reuse
            .Should().ContainSingle(r => r.Id == map.Id).Subject;
        partialReuse.Recommendation.Should().Be("Reuse");
        partialReuse.Score.Should().Be(0.33);

        var neither = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationIdentifiers = [new LocationIdentifierHint { System = "http://a", Value = "UNIT-9" }],
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = "1052-0" }]
        };
        OrgResourceMapProposalBuilder.Build(neither, [map]).Reuse
            .Should().NotContain(r => r.Id == map.Id && r.Recommendation == "Reuse");

        var complete = new BundleConfigFingerprint
        {
            LocationCount = 3,
            LocationTypes =
            [
                new LocationTypeHint { System = hsloc, Code = "1099-1" },
                new LocationTypeHint { System = hsloc, Code = "1027-2" },
                new LocationTypeHint { System = hsloc, Code = "9999-9" }
            ]
        };
        OrgResourceMapProposalBuilder.Build(complete, [map]).Reuse
            .Should().ContainSingle(r => r.Id == map.Id && r.Recommendation == "Reuse" && r.Score == 0.67);
    }

    [Fact]
    public void Orm_builder_offers_extend_when_a_type_only_map_matches_no_uploaded_code()
    {
        const string hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
        var map = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Step down 1099-1",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.exists(system = '{hsloc}' and code = '1099-1')"
                }
            ]
        };
        var upload = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = "1027-2" }]
        };
        var extend = OrgResourceMapProposalBuilder.Build(upload, [map]).Reuse
            .Should().ContainSingle(r => r.Id == map.Id).Subject;
        extend.Recommendation.Should().Be("Extend");
        extend.Score.Should().Be(0);

        var noShape = new BundleConfigFingerprint { LocationCount = 1 };
        OrgResourceMapProposalBuilder.Build(noShape, [map]).Reuse
            .Should().NotContain(r => r.Id == map.Id);
    }

    [Fact]
    public void Orm_builder_does_not_let_a_subsumed_code_lower_a_system_level_type_map()
    {
        const string hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
        var map = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Any HSLOC plus one code",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.where(system = '{hsloc}').exists()"
                },
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.where(system = '{hsloc}' and code = '1099-1').exists()"
                }
            ]
        };
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = "1027-2" }]
        };

        var reuse = OrgResourceMapProposalBuilder.Build(fp, [map]).Reuse
            .Should().ContainSingle(r => r.Id == map.Id).Subject;
        reuse.Recommendation.Should().Be("Reuse");
        reuse.Score.Should().Be(1);
    }

    [Fact]
    public void Orm_builder_does_not_reuse_type_map_when_required_alias_is_missing()
    {
        const string hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
        var map = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "ICU step down",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.exists(system = '{hsloc}' and code = '1099-1') and Location.alias = 'ICU'"
                }
            ]
        };
        var missingAlias = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
            LocationAliases = ["Ward"],
            RawLocations =
            [
                new RawLocationHint
                {
                    Types = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
                    Aliases = ["Ward"]
                }
            ]
        };
        OrgResourceMapProposalBuilder.Build(missingAlias, [map]).Reuse
            .Should().NotContain(r => r.Id == map.Id && r.Recommendation == "Reuse");

        var withAlias = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
            LocationAliases = ["ICU"],
            RawLocations =
            [
                new RawLocationHint
                {
                    Types = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
                    Aliases = ["ICU"]
                }
            ]
        };
        OrgResourceMapProposalBuilder.Build(withAlias, [map]).Reuse
            .Should().ContainSingle(r => r.Id == map.Id && r.Recommendation == "Reuse" && r.Score == 1);

        var differentCase = new BundleConfigFingerprint
        {
            LocationCount = 2,
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
            LocationAliases = ["icu", "ICU"],
            RawLocations =
            [
                new RawLocationHint
                {
                    Types = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
                    Aliases = ["icu"]
                },
                new RawLocationHint
                {
                    Types = [new LocationTypeHint { System = hsloc, Code = "1027-2" }],
                    Aliases = ["ICU"]
                }
            ]
        };
        OrgResourceMapProposalBuilder.Build(differentCase, [map]).Reuse
            .Should().NotContain(r => r.Id == map.Id && r.Recommendation == "Reuse");

        var bothCases = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Both alias cases",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.exists(system = '{hsloc}' and code = '1099-1') and Location.alias = 'ICU'"
                },
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.exists(system = '{hsloc}' and code = '1099-1') and Location.alias = 'icu'"
                }
            ]
        };
        var lowerOnly = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
            RawLocations =
            [
                new RawLocationHint
                {
                    Types = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
                    Aliases = ["icu"]
                }
            ]
        };
        OrgResourceMapProposalBuilder.Build(lowerOnly, [bothCases]).Reuse
            .Should().ContainSingle(r => r.Id == bothCases.Id && r.Recommendation == "Reuse");

        var aliasWithOr = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "ICU or Stepdown",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.exists(system = '{hsloc}' and code = '1099-1') and Location.alias = 'ICU or Stepdown'"
                }
            ]
        };
        var stepdown = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
            RawLocations =
            [
                new RawLocationHint
                {
                    Types = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
                    Aliases = ["ICU or Stepdown"]
                }
            ]
        };
        OrgResourceMapProposalBuilder.Build(stepdown, [aliasWithOr]).Reuse
            .Should().ContainSingle(r => r.Id == aliasWithOr.Id && r.Recommendation == "Reuse");
    }

    [Fact]
    public void Orm_builder_scores_system_alias_only_for_codes_on_the_matching_location()
    {
        const string hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
        var aliasMap = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "HSLOC on ICU",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.where(system = '{hsloc}').exists() and Location.alias = 'ICU'"
                }
            ]
        };
        var anyHsloc = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Any HSLOC",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.where(system = '{hsloc}').exists()"
                }
            ]
        };
        var split = new BundleConfigFingerprint
        {
            LocationCount = 2,
            LocationTypes =
            [
                new LocationTypeHint { System = hsloc, Code = "1099-1" },
                new LocationTypeHint { System = hsloc, Code = "1027-2" }
            ],
            RawLocations =
            [
                new RawLocationHint
                {
                    Types = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
                    Aliases = ["ICU"]
                },
                new RawLocationHint
                {
                    Types = [new LocationTypeHint { System = hsloc, Code = "1027-2" }],
                    Aliases = ["Ward"]
                }
            ]
        };
        var ranked = OrgResourceMapProposalBuilder.Build(split, [aliasMap, anyHsloc]).Reuse;
        ranked[0].Id.Should().Be(anyHsloc.Id);
        ranked[0].Score.Should().Be(1);
        ranked.Should().Contain(r => r.Id == aliasMap.Id && r.Recommendation == "Reuse" && r.Score == 0.5);

        var sameLocation = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationTypes =
            [
                new LocationTypeHint { System = hsloc, Code = "1099-1" },
                new LocationTypeHint { System = hsloc, Code = "1027-2" }
            ],
            RawLocations =
            [
                new RawLocationHint
                {
                    Types =
                    [
                        new LocationTypeHint { System = hsloc, Code = "1099-1" },
                        new LocationTypeHint { System = hsloc, Code = "1027-2" }
                    ],
                    Aliases = ["ICU"]
                }
            ]
        };
        OrgResourceMapProposalBuilder.Build(sameLocation, [aliasMap]).Reuse
            .Should().ContainSingle(r => r.Id == aliasMap.Id && r.Recommendation == "Reuse" && r.Score == 1);
    }

    [Fact]
    public void Orm_builder_matches_alias_literals_without_trimming()
    {
        const string hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
        OrganizationResourceMapTemplate Map(string name, string alias) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.exists(system = '{hsloc}' and code = '1099-1') and Location.alias = '{alias}'"
                }
            ]
        };
        BundleConfigFingerprint Upload(string alias) => new()
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
            LocationAliases = [alias.Trim()],
            RawLocations =
            [
                new RawLocationHint
                {
                    Types = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
                    Aliases = [alias]
                }
            ]
        };

        var plain = Map("Plain ICU", "ICU");
        var padded = Map("Padded ICU", " ICU ");
        var paddedUpload = Upload(" ICU ");
        var plainUpload = Upload("ICU");

        OrgResourceMapProposalBuilder.Build(paddedUpload, [plain]).Reuse
            .Should().NotContain(r => r.Id == plain.Id && r.Recommendation == "Reuse");
        OrgResourceMapProposalBuilder.Build(paddedUpload, [padded]).Reuse
            .Should().ContainSingle(r => r.Id == padded.Id && r.Recommendation == "Reuse" && r.Score == 1);
        OrgResourceMapProposalBuilder.Build(plainUpload, [padded]).Reuse
            .Should().NotContain(r => r.Id == padded.Id && r.Recommendation == "Reuse");
    }

    [Fact]
    public void Orm_builder_unescapes_fhirpath_alias_literals()
    {
        const string hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
        OrganizationResourceMapTemplate Map(string name, string path) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Conditions = [new OrganizationResourceMapCondition { FhirPath = path }]
        };
        BundleConfigFingerprint Upload(string alias) => new()
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
            RawLocations =
            [
                new RawLocationHint
                {
                    Types = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
                    Aliases = [alias]
                }
            ]
        };

        var slashMap = Map(
            "Slash",
            "Location.type.coding.exists(system = '" + hsloc + @"' and code = '1099-1') and Location.alias = 'ICU\\Ward'");
        OrgResourceMapProposalBuilder.Build(Upload(@"ICU\Ward"), [slashMap]).Reuse
            .Should().ContainSingle(r => r.Id == slashMap.Id && r.Recommendation == "Reuse" && r.Score == 1);
        OrgResourceMapProposalBuilder.Build(Upload(@"ICU\\Ward"), [slashMap]).Reuse
            .Should().NotContain(r => r.Id == slashMap.Id && r.Recommendation == "Reuse");

        var apostropheMap = Map(
            "Apostrophe",
            "Location.type.coding.exists(system = '" + hsloc + @"' and code = '1099-1') and Location.alias = 'O\'Brien'");
        OrgResourceMapProposalBuilder.Build(Upload("O'Brien"), [apostropheMap]).Reuse
            .Should().ContainSingle(r => r.Id == apostropheMap.Id && r.Recommendation == "Reuse" && r.Score == 1);
    }

    [Fact]
    public void Orm_builder_splits_or_after_an_alias_that_ends_in_a_backslash()
    {
        const string hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
        var map = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Slash or ward",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = "Location.type.coding.exists(system = '" + hsloc
                        + @"' and code = '1099-1') and Location.alias = 'ICU\\' or Location.type.coding.where(system = '"
                        + hsloc + "' and code = '1027-2').exists()"
                }
            ]
        };
        BundleConfigFingerprint Upload(string? alias, string code) => new()
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = code }],
            RawLocations =
            [
                new RawLocationHint
                {
                    Types = [new LocationTypeHint { System = hsloc, Code = code }],
                    Aliases = alias == null ? [] : [alias]
                }
            ]
        };

        OrgResourceMapProposalBuilder.Build(Upload(@"ICU\", "1099-1"), [map]).Reuse
            .Should().ContainSingle(r => r.Id == map.Id && r.Recommendation == "Reuse");
        OrgResourceMapProposalBuilder.Build(Upload(null, "1027-2"), [map]).Reuse
            .Should().ContainSingle(r => r.Id == map.Id && r.Recommendation == "Reuse");
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
        proposal.Reuse.Should().NotContain(r => r.Id == hospitalOnly.Id && (r.Recommendation == "Reuse" || r.Score >= 0.999));
    }

    [Fact]
    public void Orm_builder_does_not_reuse_editor_value_specific_identifier_for_a_different_value()
    {
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationIdentifiers =
            [
                new LocationIdentifierHint { System = "http://a", Value = "UNIT-9" },
                new LocationIdentifierHint { System = "http://a", Value = "UNIT-2" }
            ]
        };
        var hospitalOnly = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "DP ORM2",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = "Location.identifier.exists(system = 'http://a' and value = 'HOSP')"
                }
            ]
        };

        var proposal = OrgResourceMapProposalBuilder.Build(fp, [hospitalOnly]);
        proposal.Reuse.Should().NotContain(r => r.Id == hospitalOnly.Id);

        fp.LocationIdentifiers.Add(new LocationIdentifierHint { System = "http://a", Value = "HOSP" });
        var matched = OrgResourceMapProposalBuilder.Build(fp, [hospitalOnly]).Reuse
            .Should().ContainSingle(r => r.Id == hospitalOnly.Id).Subject;
        matched.Recommendation.Should().Be("Reuse");
        matched.Score.Should().Be(0.33);
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
    public void Orm_builder_adds_a_type_condition_for_every_uploaded_code_on_the_same_system()
    {
        const string hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 3,
            LocationsWithoutIdentifier = 3,
            LocationTypes =
            [
                new LocationTypeHint { System = hsloc, Code = "1039-7" },
                new LocationTypeHint { System = hsloc, Code = "1052-0" },
                new LocationTypeHint { System = hsloc, Code = "1060-3" }
            ]
        };

        var proposal = OrgResourceMapProposalBuilder.Build(fp, []);
        proposal.Conditions.Should().HaveCount(3);
        proposal.Conditions.Select(c => c.FhirPath).Should().BeEquivalentTo(
        [
            $"Location.type.coding.where(system = '{hsloc}' and code = '1039-7').exists()",
            $"Location.type.coding.where(system = '{hsloc}' and code = '1052-0').exists()",
            $"Location.type.coding.where(system = '{hsloc}' and code = '1060-3').exists()"
        ]);
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
        proposal.Reuse.Should().ContainSingle(r => r.Id == existing.Id && r.Recommendation == "Extend" && r.Score == 0.5);
    }

    [Fact]
    public void Orm_builder_reuses_mixed_map_when_a_type_condition_matches_despite_a_partial_identifier()
    {
        const string hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
        var map = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Hospital or stepdown",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = "Location.identifier.where(system = 'http://a' and value = 'HOSP').exists()"
                },
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.exists(system = '{hsloc}' and code = '1099-1')"
                }
            ]
        };
        var matchedType = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationIdentifiers =
            [
                new LocationIdentifierHint { System = "http://a", Value = "HOSP" },
                new LocationIdentifierHint { System = "http://b", Value = "UNIT-9" }
            ],
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = "1099-1" }]
        };
        OrgResourceMapProposalBuilder.Build(matchedType, [map]).Reuse
            .Should().ContainSingle(r => r.Id == map.Id && r.Recommendation == "Reuse");

        var missedType = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationIdentifiers =
            [
                new LocationIdentifierHint { System = "http://a", Value = "HOSP" },
                new LocationIdentifierHint { System = "http://b", Value = "UNIT-9" }
            ],
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = "1027-2" }]
        };
        OrgResourceMapProposalBuilder.Build(missedType, [map]).Reuse
            .Should().ContainSingle(r => r.Id == map.Id && r.Recommendation == "Extend" && r.Score == 0.5);
    }

    [Fact]
    public void Orm_builder_does_not_reuse_a_type_code_that_differs_only_by_case()
    {
        const string hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
        var exact = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Code ABC",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.exists(system = '{hsloc}' and code = 'ABC')"
                }
            ]
        };
        BundleConfigFingerprint Upload(string code) => new()
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = code }],
            RawLocations =
            [
                new RawLocationHint
                {
                    Types = [new LocationTypeHint { System = hsloc, Code = code }]
                }
            ]
        };

        OrgResourceMapProposalBuilder.Build(Upload("abc"), [exact]).Reuse
            .Should().NotContain(r => r.Id == exact.Id && r.Recommendation == "Reuse");
        OrgResourceMapProposalBuilder.Build(Upload("ABC"), [exact]).Reuse
            .Should().ContainSingle(r => r.Id == exact.Id && r.Recommendation == "Reuse");
    }

    [Fact]
    public void Orm_builder_matches_a_type_code_that_contains_a_pipe()
    {
        const string system = "http://t";
        var map = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Piped code",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.exists(system = '{system}' and code = 'A|B') and Location.alias = 'ICU'"
                }
            ]
        };
        var hit = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = system, Code = "A|B" }],
            RawLocations =
            [
                new RawLocationHint
                {
                    Types = [new LocationTypeHint { System = system, Code = "A|B" }],
                    Aliases = ["ICU"]
                }
            ]
        };
        OrgResourceMapProposalBuilder.Build(hit, [map]).Reuse
            .Should().ContainSingle(r => r.Id == map.Id && r.Recommendation == "Reuse");

        var swapped = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = system, Code = "A" }],
            RawLocations =
            [
                new RawLocationHint
                {
                    Types = [new LocationTypeHint { System = system, Code = "A" }],
                    Aliases = ["B|ICU"]
                }
            ]
        };
        OrgResourceMapProposalBuilder.Build(swapped, [map]).Reuse
            .Should().NotContain(r => r.Id == map.Id && r.Recommendation == "Reuse");
    }

    [Fact]
    public void Orm_builder_unescapes_identifier_and_type_literals()
    {
        const string system = "http://t";
        var identifierMap = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Slashed identifier",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = "Location.identifier.where(system = '" + system + @"' and value = 'A\\B').exists()"
                }
            ]
        };
        var identifierUpload = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationIdentifiers = [new LocationIdentifierHint { System = system, Value = @"A\B" }]
        };
        OrgResourceMapProposalBuilder.Build(identifierUpload, [identifierMap]).Reuse
            .Should().ContainSingle(r => r.Id == identifierMap.Id && r.Recommendation == "Reuse");

        var doubled = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationIdentifiers = [new LocationIdentifierHint { System = system, Value = @"A\\B" }]
        };
        OrgResourceMapProposalBuilder.Build(doubled, [identifierMap]).Reuse
            .Should().NotContain(r => r.Id == identifierMap.Id && r.Recommendation == "Reuse");

        var typeMap = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Slashed code",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = "Location.type.coding.exists(system = '" + system + @"' and code = 'A\\B')"
                }
            ]
        };
        var typeUpload = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = system, Code = @"A\B" }]
        };
        OrgResourceMapProposalBuilder.Build(typeUpload, [typeMap]).Reuse
            .Should().ContainSingle(r => r.Id == typeMap.Id && r.Recommendation == "Reuse");
    }

    [Fact]
    public void Orm_builder_does_not_reuse_an_identifier_value_that_differs_only_by_case()
    {
        var exact = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Hospital",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = "Location.identifier.where(system = 'http://a' and value = 'HOSP').exists()"
                }
            ]
        };
        var anyValue = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Any http://a",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = "Location.identifier.where(system = 'http://a').exists()"
                }
            ]
        };
        BundleConfigFingerprint Upload(string value) => new()
        {
            LocationCount = 1,
            LocationIdentifiers = [new LocationIdentifierHint { System = "http://a", Value = value }]
        };

        OrgResourceMapProposalBuilder.Build(Upload("hosp"), [exact]).Reuse
            .Should().NotContain(r => r.Id == exact.Id && r.Recommendation == "Reuse");
        OrgResourceMapProposalBuilder.Build(Upload("HOSP"), [exact]).Reuse
            .Should().ContainSingle(r => r.Id == exact.Id && r.Recommendation == "Reuse");
        OrgResourceMapProposalBuilder.Build(Upload("hosp"), [anyValue]).Reuse
            .Should().ContainSingle(r => r.Id == anyValue.Id && r.Recommendation == "Reuse");
    }

    [Fact]
    public void Orm_builder_parses_a_generated_type_code_that_contains_an_apostrophe()
    {
        var fp = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = "http://t", Code = "O'Brien" }]
        };
        var generated = OrgResourceMapProposalBuilder.Build(fp, []);
        generated.Conditions.Should().ContainSingle();
        generated.Conditions[0].FhirPath.Should().Contain("\\'");

        var saved = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Apostrophe code",
            Conditions = generated.Conditions
        };
        OrgResourceMapProposalBuilder.Build(fp, [saved]).Reuse
            .Should().ContainSingle(r => r.Id == saved.Id && r.Recommendation == "Reuse");

        var identifiers = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationIdentifiers = [new LocationIdentifierHint { System = "http://o'brien", Value = "HOSP" }]
        };
        var generatedIdentifier = OrgResourceMapProposalBuilder.Build(identifiers, []);
        generatedIdentifier.Conditions.Should().ContainSingle();
        generatedIdentifier.Conditions[0].FhirPath.Should().Contain("\\'");
        var savedIdentifier = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Apostrophe system",
            Conditions = generatedIdentifier.Conditions
        };
        OrgResourceMapProposalBuilder.Build(identifiers, [savedIdentifier]).Reuse
            .Should().ContainSingle(r => r.Id == savedIdentifier.Id && r.Recommendation == "Reuse");
    }

    [Fact]
    public void Orm_builder_does_not_reuse_a_type_map_that_requires_an_empty_alias()
    {
        const string hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
        var emptyAlias = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Empty alias",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.exists(system = '{hsloc}' and code = '1099-1') and Location.alias = ''"
                }
            ]
        };
        var plain = new OrganizationResourceMapTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Code only",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = $"Location.type.coding.exists(system = '{hsloc}' and code = '1099-1')"
                }
            ]
        };
        var upload = new BundleConfigFingerprint
        {
            LocationCount = 1,
            LocationTypes = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
            RawLocations =
            [
                new RawLocationHint
                {
                    Types = [new LocationTypeHint { System = hsloc, Code = "1099-1" }],
                    Aliases = ["ICU"]
                }
            ]
        };

        OrgResourceMapProposalBuilder.Build(upload, [emptyAlias]).Reuse
            .Should().NotContain(r => r.Id == emptyAlias.Id && r.Recommendation == "Reuse");
        OrgResourceMapProposalBuilder.Build(upload, [plain]).Reuse
            .Should().ContainSingle(r => r.Id == plain.Id && r.Recommendation == "Reuse");
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
