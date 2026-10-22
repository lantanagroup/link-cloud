using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Automation.Generation;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class NormalizationRunResultsBuilderTests
{
    [Fact]
    public void Build_reports_configured_suite_and_organization_map()
    {
        var suiteId = Guid.NewGuid();
        var mapId = Guid.NewGuid();
        var copy = Operation("Copy location", "CopyLocation", "Location");
        var sequence = new NormalizationSequenceDefinition
        {
            Name = "Locations",
            Entries = [new NormalizationSequenceEntry { OperationId = copy.Id, Sequence = 1 }]
        };
        var suite = new NormalizationSuiteDefinition
        {
            Id = suiteId,
            Name = "Epic",
            Description = "Epic suite",
            IsDefault = true,
            SequenceIds = [sequence.Id]
        };
        var map = new OrganizationResourceMapTemplate
        {
            Id = mapId,
            Name = "Epic map",
            Description = "Map",
            Conditions =
            [
                new OrganizationResourceMapCondition { Priority = 2, FhirPath = "later" },
                new OrganizationResourceMapCondition { Priority = 1, FhirPath = "first" }
            ]
        };

        var results = NormalizationRunResultsBuilder.Build(new NormalizationRunResultsSource
        {
            RunFinished = true,
            Manifest = Manifest(suiteId, mapId, ("p1", "Generated")),
            Suite = suite,
            SuiteOperations = [copy],
            SuiteSequences = [sequence],
            OrganizationMap = map
        });

        results.SuiteState.Should().Be("configured");
        results.SuiteId.Should().Be(suiteId);
        results.SuiteName.Should().Be("Epic");
        results.Suite!.Sequences.Should().ContainSingle();
        results.Suite.Sequences[0].Operations[0].Name.Should().Be("Copy location");
        results.OrganizationMapState.Should().Be("configured");
        results.OrganizationMapName.Should().Be("Epic map");
        results.OrganizationMap!.Conditions.Select(c => c.FhirPath).Should().Equal("first", "later");
        results.Operations.Should().ContainSingle();
        results.Operations[0].Name.Should().Be("Copy location");
        results.PatientSource.Should().Be("generated");
    }

    [Fact]
    public void Build_reports_not_configured_when_the_suite_name_is_none()
    {
        var results = NormalizationRunResultsBuilder.Build(new NormalizationRunResultsSource
        {
            RunFinished = true,
            Evidence = new NormalizationEvidenceSnapshot { SuiteName = "None" }
        });

        results.SuiteState.Should().Be("not-configured");
        results.SuiteId.Should().BeNull();
        results.SuiteName.Should().BeNull();
        results.OrganizationMapState.Should().Be("not-configured");
        results.PatientSource.Should().Be("unknown");
        results.GeneratedConfirmationState.Should().Be("not-applicable");
        results.UploadedConfirmationState.Should().Be("not-applicable");
        results.EvidenceState.Should().Be("none");
    }

    [Fact]
    public void Build_reports_unavailable_when_the_selected_suite_is_missing()
    {
        var suiteId = Guid.NewGuid();
        var results = NormalizationRunResultsBuilder.Build(new NormalizationRunResultsSource
        {
            RunFinished = true,
            Manifest = new GenerationManifestSnapshot
            {
                AppliedNormalizationSuiteId = suiteId,
                AppliedNormalizationSuiteName = "Retired suite"
            }
        });

        results.SuiteState.Should().Be("unavailable");
        results.SuiteId.Should().Be(suiteId);
        results.SuiteName.Should().Be("Retired suite");
        results.Suite.Should().BeNull();
    }

    [Fact]
    public void Build_confirms_generated_patients_produced_with_the_selected_suite()
    {
        var results = NormalizationRunResultsBuilder.Build(new NormalizationRunResultsSource
        {
            RunFinished = true,
            Manifest = new GenerationManifestSnapshot
            {
                AppliedNormalizationSuiteName = "Epic",
                GeneratedWithSelectedSuite = true,
                PatientOriginByPatient = new Dictionary<string, string>
                {
                    ["g1"] = "Generated",
                    ["g2"] = "Generated"
                }
            }
        });

        results.PatientSource.Should().Be("generated");
        results.GeneratedPatientCount.Should().Be(2);
        results.GeneratedConfirmationState.Should().Be("confirmed");
        results.GeneratedConfirmation.Should().Contain("Epic");
        results.GeneratedConfirmation.Should().Contain("2");
        results.UploadedConfirmationState.Should().Be("not-applicable");
    }

    [Fact]
    public void Build_confirms_uploaded_patients_from_summary_log_evidence()
    {
        var results = NormalizationRunResultsBuilder.Build(new NormalizationRunResultsSource
        {
            RunFinished = true,
            Manifest = new GenerationManifestSnapshot
            {
                AppliedNormalizationSuiteName = "Epic",
                PatientOriginByPatient = new Dictionary<string, string> { ["u1"] = "Uploaded" }
            },
            Evidence = new NormalizationEvidenceSnapshot
            {
                SuiteName = "Epic",
                CollectedLineCount = 4,
                OmittedStepCount = 2,
                EvidenceInventory = ["Encounter#1 CopyLocation 'Copy': Success=2"]
            }
        });

        results.PatientSource.Should().Be("uploaded");
        results.UploadedConfirmationState.Should().Be("confirmed");
        results.UploadedConfirmation.Should().Contain("Epic");
        results.UploadedConfirmation.Should().Contain("4");
        results.EvidenceState.Should().Be("found");
        results.EvidenceInventory.Should().Equal("Encounter#1 CopyLocation 'Copy': Success=2");
        results.ParsedStepCount.Should().Be(2);
    }

    [Fact]
    public void Build_reports_no_evidence_for_a_finished_uploaded_run()
    {
        var results = NormalizationRunResultsBuilder.Build(new NormalizationRunResultsSource
        {
            RunFinished = true,
            Manifest = new GenerationManifestSnapshot
            {
                AppliedNormalizationSuiteName = "Epic",
                PatientOriginByPatient = new Dictionary<string, string> { ["u1"] = "Uploaded" }
            },
            Evidence = new NormalizationEvidenceSnapshot { SuiteName = "Epic", EvidenceCollected = true }
        });

        results.UploadedConfirmationState.Should().Be("no-evidence");
        results.UploadedConfirmation.Should().Contain("No normalization summary log evidence");
        results.EvidenceState.Should().Be("none");
        results.EvidenceMessage.Should().Contain("No normalization execution evidence");
    }

    [Fact]
    public void Build_reports_pending_evidence_while_the_run_is_still_going()
    {
        var results = NormalizationRunResultsBuilder.Build(new NormalizationRunResultsSource
        {
            RunFinished = false,
            RunConfigurationJson = """{"patientCount":0,"importedPatientIds":["u1"],"normalizationSuiteId":"11111111-1111-1111-1111-111111111111"}"""
        });

        results.PatientSource.Should().Be("uploaded");
        results.UploadedPatientCount.Should().Be(1);
        results.UploadedConfirmationState.Should().Be("pending");
        results.EvidenceState.Should().Be("pending");
        results.EvidenceMessage.Should().Contain("has not been collected yet");
        results.SuiteState.Should().Be("unavailable");
    }

    [Fact]
    public void Build_uses_configuration_counts_for_an_older_run()
    {
        var suiteId = Guid.NewGuid();
        var mapId = Guid.NewGuid();
        var results = NormalizationRunResultsBuilder.Build(new NormalizationRunResultsSource
        {
            RunFinished = true,
            RunConfigurationJson =
                $$"""
                {"patientCount":4,"importedPatientBundles":[{},{}],"normalizationSuiteId":"{{suiteId}}","organizationResourceMapTemplateId":"{{mapId}}"}
                """
        });

        results.GeneratedPatientCount.Should().Be(4);
        results.UploadedPatientCount.Should().Be(2);
        results.PatientSource.Should().Be("mixed");
        results.GeneratedConfirmationState.Should().Be("not-recorded");
        results.UploadedConfirmationState.Should().Be("no-evidence");
        results.SuiteId.Should().Be(suiteId);
        results.OrganizationMapId.Should().Be(mapId);
        results.SuiteState.Should().Be("unavailable");
        results.OrganizationMapState.Should().Be("unavailable");
    }

    [Fact]
    public void Build_prefers_manifest_origins_over_configuration_counts()
    {
        var results = NormalizationRunResultsBuilder.Build(new NormalizationRunResultsSource
        {
            RunFinished = true,
            RunConfigurationJson = """{"patientCount":9,"importedPatientIds":["a","b","c"]}""",
            Manifest = new GenerationManifestSnapshot
            {
                PatientOriginByPatient = new Dictionary<string, string>
                {
                    ["g"] = "Generated",
                    ["u"] = "Uploaded"
                }
            }
        });

        results.GeneratedPatientCount.Should().Be(1);
        results.UploadedPatientCount.Should().Be(1);
        results.PatientSource.Should().Be("mixed");
    }

    [Fact]
    public void Build_does_not_throw_when_configuration_json_is_invalid()
    {
        var act = () => NormalizationRunResultsBuilder.Build(new NormalizationRunResultsSource
        {
            RunFinished = true,
            RunConfigurationJson = "{"
        });

        act.Should().NotThrow();
        act().PatientSource.Should().Be("unknown");
        act().SuiteState.Should().Be("not-configured");
    }

    [Fact]
    public void Build_uses_evidence_operations_ahead_of_the_stored_suite()
    {
        var copy = Operation("Stored", "CopyLocation", "Location");
        var sequence = new NormalizationSequenceDefinition
        {
            Name = "Locations",
            Entries = [new NormalizationSequenceEntry { OperationId = copy.Id, Sequence = 1 }]
        };
        var suite = new NormalizationSuiteDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Epic",
            SequenceIds = [sequence.Id]
        };

        var results = NormalizationRunResultsBuilder.Build(new NormalizationRunResultsSource
        {
            RunFinished = true,
            Suite = suite,
            SuiteOperations = [copy],
            SuiteSequences = [sequence],
            Manifest = new GenerationManifestSnapshot { AppliedNormalizationSuiteId = suite.Id },
            Evidence = new NormalizationEvidenceSnapshot
            {
                OperationConfigs =
                [
                    new NormalizationOperationConfigSnapshot
                    {
                        Name = "Ran",
                        OperationType = "CodeMap",
                        ResourceTypes = ["Encounter"]
                    }
                ]
            }
        });

        results.Operations.Should().ContainSingle();
        results.Operations[0].Name.Should().Be("Ran");
        results.Operations[0].ResourceTypes.Should().Equal("Encounter");
    }

    [Fact]
    public void Summary_keeps_sequence_order_and_notes_a_missing_operation()
    {
        var first = Operation("First", "CopyLocation", "Location");
        var second = Operation("Second", "CodeMap", "Encounter");
        var missingId = Guid.NewGuid();
        var sequence = new NormalizationSequenceDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Main",
            Entries =
            [
                new NormalizationSequenceEntry { OperationId = second.Id, Sequence = 2 },
                new NormalizationSequenceEntry { OperationId = first.Id, Sequence = 1 },
                new NormalizationSequenceEntry { OperationId = missingId, Sequence = 3 }
            ]
        };
        var missingSequenceId = Guid.NewGuid();
        var suite = new NormalizationSuiteDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Epic",
            SequenceIds = [sequence.Id, missingSequenceId],
            OperationIds = [second.Id]
        };

        var summary = NormalizationSuiteSummaryBuilder.Build(suite, [first, second], [sequence]);

        summary.Sequences.Should().HaveCount(2);
        summary.Sequences[0].Operations.Select(op => op.Name).Should().Equal("First", "Second", missingId.ToString());
        summary.Sequences[0].Operations[2].Missing.Should().BeTrue();
        summary.Sequences[1].Name.Should().Be("Missing sequence");
        summary.Sequences[1].Operations.Should().ContainSingle();
        summary.Sequences[1].Operations[0].Missing.Should().BeTrue();
        summary.Operations.Should().ContainSingle();
        summary.Operations[0].Name.Should().Be("Second");
    }

    [Fact]
    public void Apply_copies_patient_counts_and_builds_an_inventory()
    {
        var evidence = new NormalizationEvidenceSnapshot
        {
            ParsedSteps =
            [
                new NormalizationEvidenceStep
                {
                    ResourceType = "Encounter",
                    Sequence = 1,
                    OperationType = "CopyLocation",
                    OperationName = "Copy",
                    Outcome = "Success"
                }
            ]
        };
        var manifest = new GenerationManifest
        {
            PatientIds = ["g", "u"],
            TemplateCacheKeyByPatient = new Dictionary<string, string>(StringComparer.Ordinal) { ["g"] = "key" },
            PreExistingPatientIds = new HashSet<string>(StringComparer.Ordinal) { "u" },
            NormalizationSuiteAppliedToGenerator = true
        };

        NormalizationEvidenceRunContext.Apply(evidence, manifest, true);

        evidence.GeneratedPatientCount.Should().Be(1);
        evidence.UploadedPatientCount.Should().Be(1);
        evidence.GeneratorAppliedSelectedSuite.Should().BeTrue();
        evidence.EvidenceCollected.Should().BeTrue();
        evidence.EvidenceInventory.Should().Contain(line => line.Contains("Copy"));
    }

    [Fact]
    public void Apply_does_not_confirm_the_generator_without_generated_patients()
    {
        var evidence = new NormalizationEvidenceSnapshot();
        var manifest = new GenerationManifest
        {
            PatientIds = ["u"],
            PreExistingPatientIds = new HashSet<string>(StringComparer.Ordinal) { "u" }
        };

        NormalizationEvidenceRunContext.Apply(evidence, manifest, true);

        evidence.GeneratedPatientCount.Should().Be(0);
        evidence.GeneratorAppliedSelectedSuite.Should().BeFalse();
        evidence.EvidenceCollected.Should().BeTrue();
    }

    private static GenerationManifestSnapshot Manifest(Guid suiteId, Guid mapId, params (string Id, string Origin)[] patients)
        => new()
        {
            AppliedNormalizationSuiteId = suiteId,
            AppliedNormalizationSuiteName = "Epic",
            AppliedOrganizationResourceMapTemplateId = mapId,
            AppliedOrganizationResourceMapName = "Epic map",
            GeneratedWithSelectedSuite = patients.Any(patient => patient.Origin == "Generated"),
            PatientOriginByPatient = patients.ToDictionary(patient => patient.Id, patient => patient.Origin)
        };

    private static NormalizationOperationDefinition Operation(string name, string type, string resourceType)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            OperationType = type,
            ResourceTypes = [resourceType]
        };
}
