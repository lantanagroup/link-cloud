using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Hl7.Fhir.Model;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Automation.Generation.Thetis;
using LantanaGroup.Automation.Helpers;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Automation;

/// <summary>
/// Tests for <see cref="AbsSubmissionPredictor"/>: imported-bundle prediction,
/// generated-patient <see cref="AbsSubmissionPredictor.PopulateManifest"/>, and
/// org-map scoping of CQL IP windows. Gold for imported fixtures is the Run
/// Diagnostics ABS actuals, not the predictor's previous output.
/// </summary>
[Trait("Category", "UnitTests")]
public class AbsSubmissionPredictorTests
{
    private static readonly DateTime PeriodStart = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc);

    private static readonly HashSet<string> PipelineDerivedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "MeasureReport",
        "OperationOutcome",
        "Organization"
    };

    private static readonly HashSet<string> SkipAbsReplayTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "MeasureReport",
        "OperationOutcome",
        "Organization",
        "Device",
        "List"
    };

    private static readonly HashSet<string> MonthlyObservationCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "laboratory", "vital-signs", "imaging", "procedure"
    };

    private static readonly HashSet<string> HypoGlucoseLoincs = new(StringComparer.OrdinalIgnoreCase)
    {
        "2345-7", "2339-0", "41653-7", "10449-7", "10450-5"
    };

    private static readonly HashSet<string> GeneratorDiagnosticReportLoincs = new(StringComparer.OrdinalIgnoreCase)
    {
        "58410-2", "24323-8", "24331-1", "57698-3", "24357-6", "85319-2",
        "24627-2", "30954-2", "18726-0", "60567-5", "11524-6"
    };

    private static readonly int[] GeneratedPatientSeeds =
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 17, 19, 23, 29, 31, 37,
        41, 42, 43, 47, 53, 67, 89, 99, 101, 111, 222, 256, 333, 444, 555, 666,
        777, 888, 999, 1000, 1024, 2026, 4096, 5000, 7777, 9999, 12345, 20240901
    ];

    private static readonly ProfiledMeasureType[] GeneratedPatientMeasures =
    [
        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
        ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation,
        ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
    ];

    // Thetis DiagnosticReports use laboratory LOINC 11502-2, which Daily CQL
    // retrieves but the predictor does not keep (Daily SDE is COVID/flu/RSV).
    // Seed-sweep invariants therefore skip Daily.
    private static readonly ProfiledMeasureType[] GeneratedPatientInvariantMeasures =
    [
        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
        ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
    ];

    private static readonly string ThetisDumpRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Automation Thetis Bundles");

    private static readonly string MegaPatientRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        "20260901_ACH_3_14hrk4jaItgGPfBsIB16onywhnu3fSqRLEy32IXJaYNkK");

    // Narrow ORM used when replaying the mega-patient files from disk: only
    // HSLOC 1039-7 hospital roots (and their partOf children) are in-org.
    private static readonly string[] MegaPatientOrgFhirPaths =
    [
        "Location.type.coding.where(system = 'https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html' and code = '1039-7').exists()"
    ];

    [Fact]
    public void Imported_patient_predictor_keeps_imaging_observations_that_landed_in_abs()
    {
        // Run dc58502b: imported 019eb19d. ABS had 249 Observations including 14 imaging.
        // Feeding the ABS clinical artifact back through the predictor must not
        // drop imaging (the greedy Category ~ span bug).
        const string patientId = "019eb19d-249b-7ea8-8ddf-0e82340c1776";
        var bundleJson = WrapClinicalNdjsonAsBundle(ReadEmbedded("run1-imported-019eb19d.abs.ndjson"));
        var periodStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc);

        var manifest = AbsSubmissionPredictor.PredictImportedBundle(
            bundleJson, patientId, periodStart, periodEnd);
        var predicted = ClinicalCounts(manifest, patientId);

        AssertCount(predicted, "Observation", 249);
        AssertCount(predicted, "DiagnosticReport", 18);
        AssertCount(predicted, "Encounter", 7);
        AssertCount(predicted, "Location", 6);
        AssertCount(predicted, "Condition", 3);
        AssertCount(predicted, "Coverage", 1);
        AssertCount(predicted, "Medication", 4);
        AssertCount(predicted, "MedicationRequest", 4);
        AssertCount(predicted, "ServiceRequest", 21);
        AssertCount(predicted, "Specimen", 15);
        AssertCount(predicted, "Patient", 1);

        manifest.GetExpectedAbsKeysForPatient(patientId)
            .Should()
            .Contain("Observation/019eb19d-26fa-78c4-abe3-42ec76dae3e7",
                "imaging Observation that landed in ABS must stay in the predicted set");
    }

    [Fact]
    public async Task Pk_hsloc_01a0cfc5_includes_encounter_location_omitted_by_everything()
    {
        // Run a3756e88 / patient 01a0cfc5 on ehr-test. Patient/$everything returned the
        // 195 resources the manifest counted and omitted Location/15eeb878, which the
        // Encounter still references and which GET Location/{id} returns. ABS then had
        // Encounter 8 and Location 7, including that Location. The org condition is the
        // identifier system carried by every location in this graph, including the
        // unmapped root that has no partOf parent, so it matches the raw resource
        // before normalization.
        const string patientId = "01a0cfc5-e9f7-762b-a544-debb1c474a5b";
        const string missingLocationId = "15eeb878-9317-4033-9fbd-ce59258c2f6f";
        const string encounterId = "3c95b03a-b4fc-4a44-82c1-61df9d8fe7e3";
        const string brokenParentId = "b222be8d-0142-4e12-8ea0-c33bec179876";
        var periodStart = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc);
        string[] orgPaths =
        [
            "identifier.where(system = 'http://hospital.example.org/locations').exists()"
        ];

        var entries = ImportedPatientLoader.ParseBundleEntries(
            ReadEmbedded("pk-hsloc-01a0cfc5.everything.json"), patientId);
        var omitted = JsonSerializer.Deserialize<Location>(
            ReadEmbedded("pk-hsloc-01a0cfc5.location-15eeb878.json"),
            FhirSerializerOptions.ForFhirWithoutValidation());
        omitted.Should().NotBeNull();

        var reads = new List<string>();
        var added = await ReferencedLocationExpander.AppendMissingAsync(
            entries,
            (id, _) =>
            {
                reads.Add(id);
                if (string.Equals(id, missingLocationId, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(omitted);
                if (string.Equals(id, brokenParentId, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult<Location?>(null);
                throw new InvalidOperationException($"Unexpected Location read: {id}");
            },
            output: null,
            CancellationToken.None);

        added.Should().Be(1);
        reads.Should().BeEquivalentTo(new[] { missingLocationId, brokenParentId });

        var manifest = AbsSubmissionPredictor.PredictImportedBundle(
            SerializeEntries(entries),
            patientId,
            periodStart,
            periodEnd,
            organizationLocationConditionFhirPaths: orgPaths);
        var predicted = ClinicalCounts(manifest, patientId);

        AssertCount(predicted, "Encounter", 8);
        AssertCount(predicted, "Location", 7);
        AssertCount(predicted, "Condition", 2);
        AssertCount(predicted, "Coverage", 1);
        AssertCount(predicted, "DiagnosticReport", 7);
        AssertCount(predicted, "Medication", 2);
        AssertCount(predicted, "MedicationRequest", 2);
        AssertCount(predicted, "Observation", 129);
        AssertCount(predicted, "ServiceRequest", 7);
        AssertCount(predicted, "Specimen", 7);
        AssertCount(predicted, "Patient", 1);
        predicted.Should().NotContainKey("MedicationAdministration");

        var keys = manifest.GetExpectedAbsKeysForPatient(patientId);
        keys.Should().Contain($"Location/{missingLocationId}");
        keys.Should().Contain($"Encounter/{encounterId}");
        manifest.ResourceKeysByPatient[patientId].Should().Contain($"Location/{missingLocationId}");
    }

    [Fact]
    public async Task Referenced_location_read_cap_fails_the_import_when_targets_remain()
    {
        var encounter = new Encounter
        {
            Id = "enc-cap",
            Status = Encounter.EncounterStatus.Finished
        };
        for (var i = 0; i < ReferencedLocationExpander.MaxLocationReads + 25; i++)
        {
            encounter.Location.Add(new Encounter.LocationComponent
            {
                Location = new ResourceReference($"Location/missing-{i:D4}")
            });
        }

        var entries = new List<Bundle.EntryComponent>
        {
            new()
            {
                Resource = encounter,
                Request = new Bundle.RequestComponent
                {
                    Method = Bundle.HTTPVerb.PUT,
                    Url = "Encounter/enc-cap"
                }
            }
        };

        var reads = 0;
        var act = async () => await ReferencedLocationExpander.AppendMissingAsync(
            entries,
            (_, _) =>
            {
                reads++;
                return Task.FromResult<Location?>(null);
            },
            output: null,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{ReferencedLocationExpander.MaxLocationReads}-read cap*");
        reads.Should().Be(ReferencedLocationExpander.MaxLocationReads);
        entries.Should().ContainSingle();
    }

    [Fact]
    public async Task Referenced_location_read_cap_fails_when_the_last_read_enqueues_partOf()
    {
        var encounter = new Encounter
        {
            Id = "enc-chain",
            Status = Encounter.EncounterStatus.Finished
        };
        encounter.Location.Add(new Encounter.LocationComponent
        {
            Location = new ResourceReference("Location/loc-0000")
        });

        var entries = new List<Bundle.EntryComponent>
        {
            new()
            {
                Resource = encounter,
                Request = new Bundle.RequestComponent
                {
                    Method = Bundle.HTTPVerb.PUT,
                    Url = "Encounter/enc-chain"
                }
            }
        };

        var reads = new List<string>();
        var act = async () => await ReferencedLocationExpander.AppendMissingAsync(
            entries,
            (id, _) =>
            {
                reads.Add(id);
                var index = int.Parse(id["loc-".Length..], System.Globalization.CultureInfo.InvariantCulture);
                return Task.FromResult<Location?>(new Location
                {
                    Id = id,
                    PartOf = new ResourceReference($"Location/loc-{(index + 1):D4}")
                });
            },
            output: null,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*still unread*");
        var unread = $"loc-{ReferencedLocationExpander.MaxLocationReads:D4}";
        reads.Should().HaveCount(ReferencedLocationExpander.MaxLocationReads);
        reads.Should().NotContain(unread);
        entries.Select(e => e.Resource).OfType<Location>().Select(location => location.Id)
            .Should().NotContain(unread);
    }

    [Theory]
    [InlineData("Location/abc", "https://ehr-test.nhsnlink.org/fhir", true, "abc")]
    [InlineData("https://ehr-test.nhsnlink.org/fhir/Location/abc", "https://ehr-test.nhsnlink.org/fhir", true, "abc")]
    [InlineData("//ehr-test.nhsnlink.org/fhir/Location/abc", "https://ehr-test.nhsnlink.org/fhir", true, "abc")]
    [InlineData("https://other.example/fhir/Location/abc", "https://ehr-test.nhsnlink.org/fhir", false, "")]
    [InlineData("//other.example/fhir/Location/abc", "https://ehr-test.nhsnlink.org/fhir", false, "")]
    [InlineData("foo/Location/abc", "https://ehr-test.nhsnlink.org/fhir", false, "")]
    [InlineData("/other/Location/abc", "https://ehr-test.nhsnlink.org/fhir", false, "")]
    [InlineData("https://ehr-test.nhsnlink.org/another-fhir/Location/abc", "https://ehr-test.nhsnlink.org/fhir", false, "")]
    [InlineData("https://ehr-test.nhsnlink.org/fhir/extra/Location/abc", "https://ehr-test.nhsnlink.org/fhir", false, "")]
    [InlineData("Location/ab-c.d", "https://ehr-test.nhsnlink.org/fhir", true, "ab-c.d")]
    [InlineData("Location/%2e%2e%2fPatient%2f123", "https://ehr-test.nhsnlink.org/fhir", false, "")]
    [InlineData("Location/has space", "https://ehr-test.nhsnlink.org/fhir", false, "")]
    [InlineData("Location/..", "https://ehr-test.nhsnlink.org/fhir", false, "")]
    [InlineData("Location/.", "https://ehr-test.nhsnlink.org/fhir", false, "")]
    [InlineData("Location/id_underscore", "https://ehr-test.nhsnlink.org/fhir", false, "")]
    [InlineData("Location/caf\u00e9", "https://ehr-test.nhsnlink.org/fhir", false, "")]
    [InlineData("Location/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "https://ehr-test.nhsnlink.org/fhir", false, "")]
    [InlineData("Location/abc/_history/2", "https://ehr-test.nhsnlink.org/fhir", true, "abc")]
    [InlineData("https://ehr-test.nhsnlink.org/fhir/Location/abc/_history/2", "https://ehr-test.nhsnlink.org/fhir", true, "abc")]
    [InlineData("Location/abc/_history", "https://ehr-test.nhsnlink.org/fhir", false, "")]
    [InlineData("Location/abc/_history/2/extra", "https://ehr-test.nhsnlink.org/fhir", false, "")]
    [InlineData("Location/abc/_history/..", "https://ehr-test.nhsnlink.org/fhir", false, "")]
    public void Location_reference_id_stays_on_the_configured_server(
        string reference,
        string fhirBase,
        bool expected,
        string expectedId)
    {
        var parsed = ReferencedLocationExpander.TryParseLocationId(
            reference,
            new Uri(fhirBase),
            out var locationId);

        parsed.Should().Be(expected);
        locationId.Should().Be(expectedId);
    }

    [Fact]
    public async Task Preloaded_import_stops_when_already_canceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var manifest = new GenerationManifest();
        var imported = new ImportedPatientInput
        {
            Source = ImportedPatientSource.ExistingId,
            PatientId = "p-cancel",
            PreLoadedEntries =
            [
                new Bundle.EntryComponent
                {
                    Resource = new Patient { Id = "p-cancel" },
                    Request = new Bundle.RequestComponent { Method = Bundle.HTTPVerb.PUT, Url = "Patient/p-cancel" }
                }
            ]
        };

        var act = async () => await FhirGenerationPipeline.ImportAndAppendPatientAsync(
            new ConsoleAutomationOutput(),
            new LantanaGroup.Automation.FhirDataLoader("https://ehr-test.nhsnlink.org/fhir"),
            manifest,
            imported,
            [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation],
            cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        manifest.PatientIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Versioned_location_reference_is_read_as_the_logical_id()
    {
        var encounter = new Encounter
        {
            Id = "enc-ver",
            Status = Encounter.EncounterStatus.Finished
        };
        encounter.Location.Add(new Encounter.LocationComponent
        {
            Location = new ResourceReference("Location/stay/_history/3")
        });
        var entries = new List<Bundle.EntryComponent>
        {
            new()
            {
                Resource = encounter,
                Request = new Bundle.RequestComponent { Method = Bundle.HTTPVerb.PUT, Url = "Encounter/enc-ver" }
            }
        };

        var reads = new List<string>();
        await ReferencedLocationExpander.AppendMissingAsync(
            entries,
            (id, _) =>
            {
                reads.Add(id);
                return Task.FromResult<Location?>(new Location { Id = id });
            },
            output: null,
            CancellationToken.None);

        reads.Should().Equal("stay");
        encounter.Location.Single().Location.Reference.Should().Be("Location/stay");
    }

    [Fact]
    public async Task Location_ids_that_differ_only_by_case_are_both_kept()
    {
        var encounter = new Encounter
        {
            Id = "enc-case",
            Status = Encounter.EncounterStatus.Finished
        };
        encounter.Location.Add(new Encounter.LocationComponent
        {
            Location = new ResourceReference("Location/abc")
        });

        var entries = new List<Bundle.EntryComponent>
        {
            new()
            {
                Resource = new Location { Id = "ABC" },
                Request = new Bundle.RequestComponent { Method = Bundle.HTTPVerb.PUT, Url = "Location/ABC" }
            },
            new()
            {
                Resource = encounter,
                Request = new Bundle.RequestComponent { Method = Bundle.HTTPVerb.PUT, Url = "Encounter/enc-case" }
            }
        };

        var reads = new List<string>();
        var added = await ReferencedLocationExpander.AppendMissingAsync(
            entries,
            (id, _) =>
            {
                reads.Add(id);
                return Task.FromResult<Location?>(new Location { Id = id });
            },
            output: null,
            CancellationToken.None);

        reads.Should().Equal("abc");
        added.Should().Be(1);
        entries.Select(e => e.Resource).OfType<Location>().Select(location => location.Id)
            .Should().BeEquivalentTo("ABC", "abc");
    }

    [Fact]
    public async Task Absolute_same_base_location_reference_is_predicted()
    {
        const string patientId = "p-abs";
        var fhirBase = new Uri("https://ehr-test.nhsnlink.org/fhir");
        var encounter = new Encounter
        {
            Id = "enc-abs",
            Status = Encounter.EncounterStatus.Finished,
            Class = new Coding("http://terminology.hl7.org/CodeSystem/v3-ActCode", "IMP"),
            Subject = new ResourceReference($"Patient/{patientId}"),
            Period = new Period
            {
                Start = "2026-09-10T00:00:00Z",
                End = "2026-09-12T00:00:00Z"
            }
        };
        encounter.Location.Add(new Encounter.LocationComponent
        {
            Location = new ResourceReference("https://ehr-test.nhsnlink.org/fhir/Location/stay")
        });

        var entries = new List<Bundle.EntryComponent>
        {
            new()
            {
                Resource = new Patient { Id = patientId },
                Request = new Bundle.RequestComponent { Method = Bundle.HTTPVerb.PUT, Url = $"Patient/{patientId}" }
            },
            new()
            {
                Resource = encounter,
                Request = new Bundle.RequestComponent { Method = Bundle.HTTPVerb.PUT, Url = "Encounter/enc-abs" }
            }
        };

        var added = await ReferencedLocationExpander.AppendMissingAsync(
            entries,
            (id, _) =>
            {
                if (id == "stay")
                {
                    return Task.FromResult<Location?>(new Location
                    {
                        Id = "stay",
                        PartOf = new ResourceReference("//ehr-test.nhsnlink.org/fhir/Location/hospital")
                    });
                }

                if (id == "hospital")
                    return Task.FromResult<Location?>(new Location { Id = "hospital" });

                throw new InvalidOperationException($"Unexpected Location read: {id}");
            },
            output: null,
            CancellationToken.None,
            fhirBase);

        added.Should().Be(2);
        encounter.Location.Single().Location.Reference.Should().Be("Location/stay");
        var stay = entries.Select(e => e.Resource).OfType<Location>().Single(location => location.Id == "stay");
        stay.PartOf.Reference.Should().Be("Location/hospital");

        var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
            patientId,
            AbsSubmissionPredictor.IndexEntries(entries),
            sharedResourceEntries: null,
            QueryPlanDefaults.GetDefaultAsInput(),
            "2026-09-01T00:00:00Z",
            "2026-09-30T23:59:59Z");
        acquired.Should().Contain("Location/stay");
        acquired.Should().Contain("Location/hospital");

        var periodStart = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc);
        var manifest = AbsSubmissionPredictor.PredictImportedBundle(
            SerializeEntries(entries),
            patientId,
            periodStart,
            periodEnd);
        var keys = manifest.GetExpectedAbsKeysForPatient(patientId);
        keys.Should().Contain("Location/stay");
        keys.Should().Contain("Encounter/enc-abs");
    }

    [Fact]
    public void Returned_location_id_must_match_the_requested_id()
    {
        var same = ImportedPatientLoader.RequireMatchingLocationId(new Location { Id = "abc" }, "abc");
        same.Id.Should().Be("abc");

        var missing = ImportedPatientLoader.RequireMatchingLocationId(new Location(), "abc");
        missing.Id.Should().Be("abc");

        var mismatch = () => ImportedPatientLoader.RequireMatchingLocationId(new Location { Id = "ABC" }, "abc");
        mismatch.Should().Throw<InvalidOperationException>().WithMessage("*Location/ABC*Location/abc*");
    }

    [Fact]
    public void Deleted_location_read_is_treated_as_missing()
    {
        LantanaGroup.Automation.FhirDataLoader.IsAbsentResource(System.Net.HttpStatusCode.NotFound).Should().BeTrue();
        LantanaGroup.Automation.FhirDataLoader.IsAbsentResource(System.Net.HttpStatusCode.Gone).Should().BeTrue();
        LantanaGroup.Automation.FhirDataLoader.IsAbsentResource(System.Net.HttpStatusCode.InternalServerError).Should().BeFalse();
    }

    [Fact]
    public void Imported_patient_predictor_excludes_diagnostic_reports_outside_ip_window()
    {
        // Run df6f9b8e: mega AddById patient. DA acquired 360 DiagnosticReports;
        // ABS kept 255 whose effective overlapped the IMP encounter (Jul 9-25).
        // Reconstruct the acquired set by adding the 105 missing DR ids dated in
        // the report period but outside the IP window.
        const string patientId = "Mpkn4SO0V9DoLQiKgv181TGGn7Lsjrio9f5ibdJizVK8o";
        var bundleJson = WrapClinicalNdjsonAsBundle(ReadEmbedded("run2-mega-Mpkn4SO0.abs.ndjson"));
        var entries = ImportedPatientLoader.ParseBundleEntries(bundleJson, patientId);
        AppendOutOfIpDiagnosticReports(entries, ReadEmbeddedJsonArray("run2-missing-diagnosticreport-ids.json"));

        var periodStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2026, 8, 31, 23, 59, 59, DateTimeKind.Utc);
        var measures = new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation };
        var queryPlan = QueryPlanDefaults.GetDefaultAsInput();
        var acquisition = new FhirGenerationPipeline.AcquisitionSimulationConfig
        {
            QueryPlan = queryPlan,
            ClinicalPeriodStart = "2026-07-01T00:00:00Z",
            ClinicalPeriodEnd = "2026-08-31T23:59:59Z"
        };

        var builder = new GenerationManifest.IncrementalBuilder();
        var profile = new PatientProfile(
            measures.ToDictionary(m => m, _ => MeasureEligibility.Qualifying));
        AbsSubmissionPredictor.PopulateManifest(
            builder,
            patientId,
            profile,
            entries,
            measures,
            acquisition,
            periodStart,
            periodEnd,
            sharedSimEntries: null,
            output: null);

        var manifest = builder.Build(measures);
        manifest.AcquiredResourceTypes = QueryPlanDefaults.GetAcquiredResourceTypes(queryPlan);
        manifest.CqlReferencedResourceTypes = CqlResourceTypeExtractor.ExtractForMeasures(measures);
        var predicted = ClinicalCounts(manifest, patientId);

        AssertCount(predicted, "DiagnosticReport", 255);
        AssertCount(predicted, "Observation", 24);
        AssertCount(predicted, "Condition", 11);
        AssertCount(predicted, "Encounter", 1);
        AssertCount(predicted, "Location", 1);
        AssertCount(predicted, "Medication", 8);
        AssertCount(predicted, "MedicationRequest", 8);
        AssertCount(predicted, "ServiceRequest", 84);
        AssertCount(predicted, "Procedure", 1);
        AssertCount(predicted, "Patient", 1);
    }

    [Fact]
    public void Org_map_scopes_cql_ip_windows_so_unlinked_diagnostic_reports_outside_org_encounters_are_excluded()
    {
        // Mega-patient shape: ORM keeps one IMP encounter; a second IMP at a
        // non-org location covers the whole report period. DiagnosticReports have
        // no encounter reference (DA still forwards them). MeasureEval IP is the
        // org encounter only, so CQL `effective overlaps IP.period` must use that
        // window — not the non-org IMP.
        const string patientId = "org-scope-patient";
        const string orgCondition =
            "Location.identifier.where(system='urn:test:loc' and value='org-root').exists()";
        var bundleJson = """
            {
              "resourceType":"Bundle",
              "type":"collection",
              "entry":[
                {"resource":{"resourceType":"Patient","id":"org-scope-patient"}},
                {"resource":{"resourceType":"Location","id":"L-ORG",
                  "identifier":[{"system":"urn:test:loc","value":"org-root"}]}},
                {"resource":{"resourceType":"Location","id":"L-OTHER",
                  "identifier":[{"system":"urn:test:loc","value":"other"}]}},
                {"resource":{"resourceType":"Encounter","id":"E-ORG",
                  "status":"in-progress",
                  "class":{"system":"http://terminology.hl7.org/CodeSystem/v3-ActCode","code":"IMP"},
                  "subject":{"reference":"Patient/org-scope-patient"},
                  "period":{"start":"2026-07-12T06:00:00Z","end":"2026-07-29T14:00:00Z"},
                  "location":[{"location":{"reference":"Location/L-ORG"}}]}},
                {"resource":{"resourceType":"Encounter","id":"E-OTHER",
                  "status":"in-progress",
                  "class":{"system":"http://terminology.hl7.org/CodeSystem/v3-ActCode","code":"IMP"},
                  "subject":{"reference":"Patient/org-scope-patient"},
                  "period":{"start":"2026-07-01T00:00:00Z","end":"2026-07-31T23:59:59Z"},
                  "location":[{"location":{"reference":"Location/L-OTHER"}}]}},
                {"resource":{"resourceType":"DiagnosticReport","id":"DR-IN",
                  "status":"final",
                  "code":{"coding":[{"system":"http://loinc.org","code":"58410-2"}]},
                  "subject":{"reference":"Patient/org-scope-patient"},
                  "effectiveDateTime":"2026-07-20T12:00:00Z"}},
                {"resource":{"resourceType":"DiagnosticReport","id":"DR-OUT",
                  "status":"final",
                  "code":{"coding":[{"system":"http://loinc.org","code":"58410-2"}]},
                  "subject":{"reference":"Patient/org-scope-patient"},
                  "effectiveDateTime":"2026-07-03T12:00:00Z"}},
                {"resource":{"resourceType":"MedicationRequest","id":"MR-IN",
                  "status":"active","intent":"order",
                  "medicationCodeableConcept":{"coding":[{"system":"http://www.nlm.nih.gov/research/umls/rxnorm","code":"197361"}]},
                  "subject":{"reference":"Patient/org-scope-patient"},
                  "authoredOn":"2026-07-20T12:00:00Z"}},
                {"resource":{"resourceType":"MedicationRequest","id":"MR-OUT",
                  "status":"active","intent":"order",
                  "medicationCodeableConcept":{"coding":[{"system":"http://www.nlm.nih.gov/research/umls/rxnorm","code":"197361"}]},
                  "subject":{"reference":"Patient/org-scope-patient"},
                  "authoredOn":"2026-07-03T12:00:00Z"}}
              ]
            }
            """;

        var manifest = AbsSubmissionPredictor.PredictImportedBundle(
            bundleJson,
            patientId,
            PeriodStart,
            PeriodEnd,
            organizationLocationConditionFhirPaths: [orgCondition]);
        AssertOrgScopedCqlKeys(manifest, patientId);

        // Live generation passes run measure JSON into PopulateManifest. Same
        // org-scope must hold when CQL reads those bundles instead of the catalog.
        var measures = new[] { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation };
        var builder = new GenerationManifest.IncrementalBuilder();
        var queryPlan = QueryPlanDefaults.GetDefaultAsInput();
        AbsSubmissionPredictor.PopulateManifest(
            builder,
            patientId,
            new PatientProfile(measures.ToDictionary(m => m, _ => MeasureEligibility.Qualifying)),
            ImportedPatientLoader.ParseBundleEntries(bundleJson, patientId),
            measures,
            new FhirGenerationPipeline.AcquisitionSimulationConfig
            {
                QueryPlan = queryPlan,
                ClinicalPeriodStart = PeriodStart.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ClinicalPeriodEnd = PeriodEnd.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                OrganizationLocationConditionFhirPaths = [orgCondition]
            },
            PeriodStart,
            PeriodEnd,
            sharedSimEntries: null,
            output: null,
            measureBundleJsons: [ProfiledMeasureCatalog.ReadBundleJson(measures[0])]);
        var liveManifest = builder.Build(measures);
        liveManifest.AcquiredResourceTypes = QueryPlanDefaults.GetAcquiredResourceTypes(queryPlan);
        liveManifest.CqlReferencedResourceTypes = CqlResourceTypeExtractor.ExtractForMeasures(measures);
        AssertOrgScopedCqlKeys(liveManifest, patientId);
    }

    [Fact]
    public async Task Generated_patients_across_seeds_match_cql_invariants_per_measure()
    {
        var failures = new List<string>();
        var (ids, sharedEntries, practitionerIds, medicationIds) = FhirBundleGenerator.BuildSharedResources();
        var sharedSim = AbsSubmissionPredictor.IndexEntries(sharedEntries);
        var queryPlan = QueryPlanDefaults.GetDefaultAsInput();

        foreach (var seed in GeneratedPatientSeeds)
        {
            var patientId = ids.PatientId(0);
            List<Bundle.EntryComponent> entries;
            try
            {
                entries = await GenerateThetisPatientEntriesAsync(
                    patientId,
                    ids,
                    practitionerIds,
                    medicationIds,
                    totalResourcesPerPatient: 220,
                    seed: seed);
            }
            catch (Exception ex)
            {
                failures.Add($"seed={seed} generate threw {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            var cqlInput = CqlFilterInputExtractor.ExtractFromEntries(patientId, entries, sharedSim);
            if (cqlInput == null)
            {
                failures.Add($"seed={seed} extractor returned null (no encounter)");
                continue;
            }

            cqlInput = cqlInput with
            {
                MeasurementPeriodStart = PeriodStart,
                MeasurementPeriodEnd = PeriodEnd
            };

            foreach (var measure in GeneratedPatientInvariantMeasures)
            {
                try
                {
                    AssertCqlInvariants(seed, measure, entries, cqlInput, failures);
                    AssertPredictorCompletes(seed, measure, patientId, entries, sharedSim, queryPlan, failures);
                }
                catch (Exception ex)
                {
                    failures.Add($"seed={seed} {ShortName(measure)} threw {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures.Take(40)));
    }

    [Fact]
    public async Task Same_generated_patient_predicted_counts_differ_by_measure_family()
    {
        var (ids, sharedEntries, practitionerIds, medicationIds) = FhirBundleGenerator.BuildSharedResources();
        var sharedSim = AbsSubmissionPredictor.IndexEntries(sharedEntries);
        var patientId = ids.PatientId(0);
        var entries = await GenerateThetisPatientEntriesAsync(
            patientId, ids, practitionerIds, medicationIds,
            totalResourcesPerPatient: 400,
            seed: 42);

        var monthly = PredictGenerated(
            patientId, entries, sharedSim, ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation);
        var daily = PredictGenerated(
            patientId, entries, sharedSim, ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation);
        var hypo = PredictGenerated(
            patientId, entries, sharedSim, ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);

        var monthlyObs = CountType(monthly, patientId, "Observation");
        var dailyObs = CountType(daily, patientId, "Observation");
        var hypoObs = CountType(hypo, patientId, "Observation");
        var monthlyDr = CountType(monthly, patientId, "DiagnosticReport");
        var dailyDr = CountType(daily, patientId, "DiagnosticReport");
        var hypoDr = CountType(hypo, patientId, "DiagnosticReport");

        dailyObs.Should().BeGreaterThanOrEqualTo(monthlyObs,
            "Daily SDE All Observations keeps categories Monthly drops (social-history/survey)");
        monthlyObs.Should().BeGreaterThan(hypoObs,
            "Hypo only keeps blood-glucose LOINCs; Monthly keeps lab/vitals/imaging/procedure");
        monthlyDr.Should().BeGreaterThan(dailyDr,
            "Daily has no all-DiagnosticReport SDE; generator panels are not COVID/flu/RSV LOINCs");
        dailyDr.Should().Be(0,
            "generated DiagnosticReport LOINCs are CBC/CMP/etc., not Daily COVID/flu/RSV valuesets");
        hypoDr.Should().Be(0,
            "Hypoglycemic CQL has no DiagnosticReport SDE retrieve");
    }

    [Fact]
    public void Submitted_thetis_dumps_predictor_matches_abs_actuals()
    {
        if (!Directory.Exists(ThetisDumpRoot))
            return;

        var failures = new List<string>();
        foreach (var folder in Directory.GetDirectories(ThetisDumpRoot).OrderBy(Path.GetFileName))
        {
            var name = Path.GetFileName(folder);
            if (name is null || !char.IsDigit(name[0]))
                continue;

            var genPath = Path.Combine(folder, "generation.json");
            var bundlePath = Path.Combine(folder, "patient-bundle.json");
            var manifestPath = Path.Combine(folder, "RunDiagnostics", "RunManifest.txt");
            if (!File.Exists(genPath) || !File.Exists(bundlePath) || !File.Exists(manifestPath))
                continue;

            var gen = JsonDocument.Parse(File.ReadAllText(genPath));
            var patientId = gen.RootElement.GetProperty("patientId").GetString()!;
            var manifestText = File.ReadAllText(manifestPath);
            if (!manifestText.Contains("submission=Submitted", StringComparison.Ordinal)
                && !manifestText.Contains($"{patientId}.ndjson  PRESENT", StringComparison.Ordinal))
            {
                continue;
            }

            var actuals = ParseAbsActuals(manifestText);
            if (actuals.Count == 0)
                continue;

            var measures = ParseMeasures(gen.RootElement);
            var periodStart = DateTime.Parse(gen.RootElement.GetProperty("periodStart").GetString()!).ToUniversalTime();
            var periodEnd = DateTime.Parse(gen.RootElement.GetProperty("periodEnd").GetString()!).ToUniversalTime();
            var bundleJson = File.ReadAllText(bundlePath);

            GenerationManifest predictedManifest;
            try
            {
                predictedManifest = AbsSubmissionPredictor.PredictImportedBundle(
                    bundleJson, patientId, periodStart, periodEnd, measures);
            }
            catch (Exception ex)
            {
                failures.Add($"{name}: predictor threw {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            var predicted = predictedManifest.GetExpectedAbsCountsForPatient(patientId)
                            ?? new Dictionary<string, int>();
            var qualifies = predictedManifest.GetExpectedAbsKeysForPatient(patientId).Count > 0;
            if (!qualifies)
            {
                failures.Add($"{name}: predictor treated submitted patient as NQ (zero expected keys)");
                continue;
            }

            foreach (var (type, actual) in actuals.OrderBy(kv => kv.Key))
            {
                if (type.Equals("OperationOutcome", StringComparison.OrdinalIgnoreCase)
                    || type.Equals("Organization", StringComparison.OrdinalIgnoreCase)
                    || type.Equals("Device", StringComparison.OrdinalIgnoreCase)
                    || type.Equals("List", StringComparison.OrdinalIgnoreCase))
                    continue;
                predicted.TryGetValue(type, out var exp);
                if (exp != actual)
                    failures.Add($"{name} {type}: predicted {exp} ABS {actual} (delta {actual - exp:+0;-0})");
            }
        }

        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Imported_mega_patient_with_narrow_org_map_matches_abs_diagnostic_report_and_medreq()
    {
        const string patientId = "v9vVDGVXbHjGRe6K7xb2iwn6a8JF3qCjz1vKaQOCzllKy";
        var bundlePath = Path.Combine(
            MegaPatientRoot,
            "20260901_ACH_1_v9vVDGVXbHjGRe6K7xb2iwn6a8JF3qCjz1vKaQOCzllKy.json");
        if (!ShouldReplayMega(bundlePath))
            return;

        var predicted = PredictImportedFile(bundlePath, patientId, MegaPatientOrgFhirPaths);
        predicted["DiagnosticReport"].Should().Be(259, "ABS actual DiagnosticReport count");
        predicted["MedicationRequest"].Should().Be(15, "ABS actual MedicationRequest count");
        predicted["Encounter"].Should().Be(2, "ABS actual Encounter count");
    }

    [Fact]
    public void Imported_mega_patient_with_narrow_org_map_matches_abs_diagnostic_report()
    {
        const string patientId = "14hrk4jaItgGPfBsIB16onywhnu3fSqRLEy32IXJaYNkK";
        var bundlePath = Path.Combine(
            MegaPatientRoot,
            "20260901_ACH_3_14hrk4jaItgGPfBsIB16onywhnu3fSqRLEy32IXJaYNkK.json");
        if (!ShouldReplayMega(bundlePath))
            return;

        var predicted = PredictImportedFile(bundlePath, patientId, MegaPatientOrgFhirPaths);
        predicted["DiagnosticReport"].Should().Be(326, "ABS actual DiagnosticReport count");
        predicted["MedicationRequest"].Should().Be(11, "ABS actual MedicationRequest count");
        predicted["Encounter"].Should().Be(1, "ABS actual Encounter count");
    }

    private static bool ShouldReplayMega(string bundlePath)
        => File.Exists(bundlePath)
           && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MEGA_QA_REPLAY"));

    private static Dictionary<string, int> PredictImportedFile(
        string bundlePath,
        string patientId,
        IReadOnlyList<string> orgFhirPaths)
    {
        var manifest = AbsSubmissionPredictor.PredictImportedBundle(
            File.ReadAllText(bundlePath),
            patientId,
            PeriodStart,
            PeriodEnd,
            organizationLocationConditionFhirPaths: orgFhirPaths);
        return manifest.GetExpectedAbsCountsForPatient(patientId)
               ?? throw new InvalidOperationException(patientId);
    }

    private static void AssertOrgScopedCqlKeys(GenerationManifest manifest, string patientId)
    {
        var predicted = ClinicalCounts(manifest, patientId);
        var keys = manifest.GetExpectedAbsKeysForPatient(patientId);

        AssertCount(predicted, "Encounter", 1);
        AssertCount(predicted, "DiagnosticReport", 1);
        AssertCount(predicted, "MedicationRequest", 1);
        keys.Should().Contain("Encounter/E-ORG");
        keys.Should().NotContain("Encounter/E-OTHER");
        keys.Should().Contain("DiagnosticReport/DR-IN");
        keys.Should().NotContain("DiagnosticReport/DR-OUT");
        keys.Should().Contain("MedicationRequest/MR-IN");
        keys.Should().NotContain("MedicationRequest/MR-OUT");
    }

    private static void AssertCount(Dictionary<string, int> predicted, string resourceType, int expected)
    {
        predicted.Should().ContainKey(resourceType);
        predicted[resourceType].Should().Be(expected, $"ABS actual count for {resourceType}");
    }

    private static Dictionary<string, int> ClinicalCounts(GenerationManifest manifest, string patientId)
    {
        var counts = manifest.GetExpectedAbsCountsForPatient(patientId)
                     ?? throw new InvalidOperationException($"No ABS prediction for {patientId}.");
        return counts
            .Where(kv => !PipelineDerivedTypes.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static string SerializeEntries(List<Bundle.EntryComponent> entries)
    {
        var bundle = new Bundle
        {
            Type = Bundle.BundleType.Collection,
            Entry = entries
        };
        return JsonSerializer.Serialize(bundle, FhirSerializerOptions.ForFhirWithoutValidation());
    }

    private static string WrapClinicalNdjsonAsBundle(string ndjson)
    {
        var parts = new List<string>();
        using var reader = new StringReader(ndjson);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;

            using var doc = JsonDocument.Parse(trimmed);
            var type = doc.RootElement.TryGetProperty("resourceType", out var rt)
                ? rt.GetString()
                : null;
            if (string.IsNullOrEmpty(type) || SkipAbsReplayTypes.Contains(type))
                continue;

            parts.Add("{\"resource\":" + trimmed + "}");
        }

        return "{\"resourceType\":\"Bundle\",\"type\":\"collection\",\"entry\":[" +
               string.Join(",", parts) + "]}";
    }

    private static void AppendOutOfIpDiagnosticReports(
        List<Bundle.EntryComponent> entries,
        IReadOnlyList<string> missingIds)
    {
        var template = entries.Select(e => e.Resource).OfType<DiagnosticReport>().First();
        foreach (var id in missingIds)
        {
            var report = new DiagnosticReport
            {
                Id = id,
                Status = DiagnosticReport.DiagnosticReportStatus.Final,
                Code = template.Code,
                Subject = template.Subject,
                Effective = new FhirDateTime("2026-08-15T12:00:00Z")
            };
            entries.Add(new Bundle.EntryComponent
            {
                Resource = report,
                Request = new Bundle.RequestComponent
                {
                    Method = Bundle.HTTPVerb.PUT,
                    Url = $"DiagnosticReport/{id}"
                }
            });
        }
    }

    private static string ReadEmbedded(string fileName)
    {
        var assembly = typeof(AbsSubmissionPredictorTests).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new FileNotFoundException(resourceName);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static List<string> ReadEmbeddedJsonArray(string fileName)
        => JsonSerializer.Deserialize<List<string>>(ReadEmbedded(fileName))
           ?? throw new InvalidOperationException(fileName);

    private static List<ProfiledMeasureType> ParseMeasures(JsonElement gen)
    {
        var measures = new List<ProfiledMeasureType>();
        foreach (var m in gen.GetProperty("runMeasures").EnumerateArray())
        {
            if (Enum.TryParse<ProfiledMeasureType>(m.GetString(), out var parsed))
                measures.Add(parsed);
        }

        return measures.Count > 0
            ? measures
            : [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation];
    }

    private static Dictionary<string, int> ParseAbsActuals(string manifestText)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var inTable = false;
        foreach (var raw in manifestText.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Contains("EXPECTED vs ACTUAL (per resource type)", StringComparison.Ordinal))
            {
                inTable = true;
                continue;
            }

            if (!inTable)
                continue;
            if (line.Contains("Type", StringComparison.Ordinal) && line.Contains("Expected"))
                continue;
            if (line.TrimStart().StartsWith("----", StringComparison.Ordinal))
                continue;
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("--", StringComparison.Ordinal))
                break;

            var match = Regex.Match(line, @"^\s+(\S+)\s+(\d+)\s+(\d+)\s+");
            if (!match.Success)
                continue;
            result[match.Groups[1].Value] = int.Parse(match.Groups[3].Value);
        }

        return result;
    }

    private static void AssertCqlInvariants(
        int seed,
        ProfiledMeasureType measure,
        List<Bundle.EntryComponent> entries,
        CqlFilterSimulator.PatientCqlInput cqlInput,
        List<string> failures)
    {
        var excluded = CqlFilterSimulator.ComputeFilteredKeys([measure], cqlInput);
        var ip = cqlInput.Encounters.FirstOrDefault(e =>
            string.Equals(e.ClassCode, "IMP", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.ClassCode, "ACUTE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.ClassCode, "EMER", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.ClassCode, "OBSENC", StringComparison.OrdinalIgnoreCase));
        if (ip == null)
        {
            failures.Add($"seed={seed} {ShortName(measure)} no inpatient-class encounter");
            return;
        }

        foreach (var entry in entries)
        {
            switch (entry.Resource)
            {
                case Observation obs:
                    CheckObservation(seed, measure, obs, ip, excluded, failures);
                    break;
                case DiagnosticReport report:
                    CheckDiagnosticReport(seed, measure, report, ip, excluded, failures);
                    break;
                case Condition condition:
                    CheckCondition(seed, measure, condition, ip, excluded, failures);
                    break;
            }
        }
    }

    private static void CheckObservation(
        int seed,
        ProfiledMeasureType measure,
        Observation obs,
        CqlFilterSimulator.EncounterContext ip,
        HashSet<string> excluded,
        List<string> failures)
    {
        var key = $"Observation/{obs.Id}";
        var category = obs.Category?.SelectMany(c => c.Coding ?? [])
            .Select(c => c.Code)
            .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? string.Empty;
        var loinc = obs.Code?.Coding?.FirstOrDefault(c =>
            string.Equals(c.System, "http://loinc.org", StringComparison.OrdinalIgnoreCase))?.Code ?? string.Empty;
        var effective = ParseEffective(obs.Effective);
        var overlapsIp = Overlaps(effective.Start, effective.End, ip.PeriodStart, ip.PeriodEnd);
        var duringIp = During(effective.Start, effective.End, ip.PeriodStart, ip.PeriodEnd);

        var kept = !excluded.Contains(key);
        bool? shouldKeep = measure switch
        {
            ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation => true,
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation when
                MonthlyObservationCategories.Contains(category) => overlapsIp,
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation when
                category.Equals("social-history", StringComparison.OrdinalIgnoreCase)
                || category.Equals("survey", StringComparison.OrdinalIgnoreCase) => false,
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation when
                HypoGlucoseLoincs.Contains(loinc) && duringIp => true,
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation when
                !HypoGlucoseLoincs.Contains(loinc) => false,
            _ => null
        };

        if (shouldKeep is { } expected && kept != expected)
        {
            failures.Add(
                $"seed={seed} {ShortName(measure)} {key} cat={category} loinc={loinc} " +
                $"predictedKept={kept} cqlShouldKeep={expected}");
        }
    }

    private static void CheckDiagnosticReport(
        int seed,
        ProfiledMeasureType measure,
        DiagnosticReport report,
        CqlFilterSimulator.EncounterContext ip,
        HashSet<string> excluded,
        List<string> failures)
    {
        var key = $"DiagnosticReport/{report.Id}";
        var code = report.Code?.Coding?.FirstOrDefault()?.Code ?? string.Empty;
        var effective = ParseEffective(report.Effective);
        var overlapsIp = Overlaps(effective.Start, effective.End, ip.PeriodStart, ip.PeriodEnd);

        var shouldKeep = measure switch
        {
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation => overlapsIp,
            ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation =>
                !GeneratorDiagnosticReportLoincs.Contains(code),
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation => true,
            _ => true
        };

        var kept = !excluded.Contains(key);
        if (measure == ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation)
        {
            if (excluded.Contains(key))
                failures.Add($"seed={seed} Hypo excluded {key} but CQL has no DiagnosticReport SDE");
            return;
        }

        if (kept != shouldKeep)
        {
            failures.Add(
                $"seed={seed} {ShortName(measure)} {key} code={code} " +
                $"predictedKept={kept} cqlShouldKeep={shouldKeep}");
        }
    }

    private static void CheckCondition(
        int seed,
        ProfiledMeasureType measure,
        Condition condition,
        CqlFilterSimulator.EncounterContext ip,
        HashSet<string> excluded,
        List<string> failures)
    {
        if (measure == ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation)
            return;

        var key = $"Condition/{condition.Id}";
        var categories = (condition.Category ?? [])
            .SelectMany(c => c.Coding ?? [])
            .Select(c => c.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .ToList();
        var isEncounterDx = categories.Any(c =>
            c.Equals("encounter-diagnosis", StringComparison.OrdinalIgnoreCase));
        var encounterId = condition.Encounter?.Reference?.Split('/').LastOrDefault() ?? string.Empty;
        var listedOnIp = ip.DiagnosisConditionIds.Any(r =>
            string.Equals(r.Split('/').LastOrDefault(), condition.Id, StringComparison.OrdinalIgnoreCase));
        var linkedToIp = string.Equals(encounterId, ip.EncounterId, StringComparison.OrdinalIgnoreCase) || listedOnIp;
        var onset = ParseEffective(condition.Onset is Period or FhirDateTime ? condition.Onset : null);
        if (onset.Start == DateTime.MinValue)
            onset = (condition.RecordedDateElement != null
                ? ParseEffective(condition.RecordedDateElement)
                : (DateTime.MinValue, DateTime.MaxValue));

        var shouldKeep = measure switch
        {
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation =>
                isEncounterDx && linkedToIp,
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation =>
                isEncounterDx || Overlaps(onset.Start, onset.End, ip.PeriodStart, ip.PeriodEnd),
            _ => true
        };

        var kept = !excluded.Contains(key);
        if (kept != shouldKeep)
        {
            failures.Add(
                $"seed={seed} {ShortName(measure)} {key} cats=[{string.Join(',', categories)}] " +
                $"predictedKept={kept} cqlShouldKeep={shouldKeep}");
        }
    }

    private static void AssertPredictorCompletes(
        int seed,
        ProfiledMeasureType measure,
        string patientId,
        List<Bundle.EntryComponent> entries,
        List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)> sharedSim,
        QueryPlanInput queryPlan,
        List<string> failures)
    {
        var profile = new PatientProfile(new Dictionary<ProfiledMeasureType, MeasureEligibility>
        {
            [measure] = MeasureEligibility.Qualifying
        });
        var builder = new GenerationManifest.IncrementalBuilder();
        AbsSubmissionPredictor.PopulateManifest(
            builder,
            patientId,
            profile,
            entries,
            [measure],
            new FhirGenerationPipeline.AcquisitionSimulationConfig
            {
                QueryPlan = queryPlan,
                ClinicalPeriodStart = PeriodStart.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ClinicalPeriodEnd = PeriodEnd.ToString("yyyy-MM-ddTHH:mm:ssZ")
            },
            PeriodStart,
            PeriodEnd,
            sharedSim,
            output: null);
        var manifest = builder.Build([measure]);
        manifest.AcquiredResourceTypes = QueryPlanDefaults.GetAcquiredResourceTypes(queryPlan);
        manifest.CqlReferencedResourceTypes = CqlResourceTypeExtractor.ExtractForMeasures([measure]);
        var keys = manifest.GetExpectedAbsKeysForPatient(patientId);
        if (keys.Count == 0)
            failures.Add($"seed={seed} {ShortName(measure)} predicted zero ABS keys for a qualifying generated patient");
        if (!keys.Contains($"Patient/{patientId}"))
            failures.Add($"seed={seed} {ShortName(measure)} missing Patient/{patientId} in predicted ABS keys");
    }

    private static Task<List<Bundle.EntryComponent>> GenerateThetisPatientEntriesAsync(
        string patientId,
        FhirBundleGenerator.SharedIds ids,
        List<string> practitionerIds,
        List<string> medicationIds,
        int totalResourcesPerPatient,
        int seed)
    {
        var request = new PatientEntryRequest
        {
            Profile = new PatientProfile(
                GeneratedPatientMeasures.ToDictionary(m => m, _ => MeasureEligibility.Qualifying)),
            PatientIndex = 0,
            BaseSeed = seed,
            TotalResourcesPerPatient = totalResourcesPerPatient,
            SharedPractitionerIds = practitionerIds,
            SharedMedicationIds = medicationIds,
            Measures = GeneratedPatientMeasures,
            ClinicalPeriodStart = PeriodStart,
            ClinicalPeriodEnd = PeriodEnd,
            Config = new FhirGenerationConfig(),
            Ids = ids,
            PatientId = patientId
        };
        return ThetisPatientEntryGenerator.Shared.GenerateAsync(request);
    }

    private static GenerationManifest PredictGenerated(
        string patientId,
        List<Bundle.EntryComponent> entries,
        List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)> sharedSim,
        ProfiledMeasureType measure)
    {
        var profile = new PatientProfile(new Dictionary<ProfiledMeasureType, MeasureEligibility>
        {
            [measure] = MeasureEligibility.Qualifying
        });
        var builder = new GenerationManifest.IncrementalBuilder();
        var queryPlan = QueryPlanDefaults.GetDefaultAsInput();
        AbsSubmissionPredictor.PopulateManifest(
            builder,
            patientId,
            profile,
            entries,
            [measure],
            new FhirGenerationPipeline.AcquisitionSimulationConfig
            {
                QueryPlan = queryPlan,
                ClinicalPeriodStart = PeriodStart.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ClinicalPeriodEnd = PeriodEnd.ToString("yyyy-MM-ddTHH:mm:ssZ")
            },
            PeriodStart,
            PeriodEnd,
            sharedSim,
            output: null);
        var manifest = builder.Build([measure]);
        manifest.AcquiredResourceTypes = QueryPlanDefaults.GetAcquiredResourceTypes(queryPlan);
        manifest.CqlReferencedResourceTypes = CqlResourceTypeExtractor.ExtractForMeasures([measure]);
        return manifest;
    }

    private static int CountType(GenerationManifest manifest, string patientId, string resourceType)
    {
        var counts = manifest.GetExpectedAbsCountsForPatient(patientId);
        if (counts == null)
            return 0;
        return counts.TryGetValue(resourceType, out var count) ? count : 0;
    }

    private static string ShortName(ProfiledMeasureType measure) => measure switch
    {
        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation => "Monthly",
        ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation => "Daily",
        ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation => "Hypo",
        _ => measure.ToString()
    };

    private static bool Overlaps(DateTime start, DateTime end, DateTime ipStart, DateTime ipEnd)
        => start <= ipEnd && end >= ipStart;

    private static bool During(DateTime start, DateTime end, DateTime ipStart, DateTime ipEnd)
        => start >= ipStart && end <= ipEnd;

    private static (DateTime Start, DateTime End) ParseEffective(DataType? effective)
    {
        switch (effective)
        {
            case Period p:
                var start = DateTime.TryParse(p.Start, out var ps) ? ps.ToUniversalTime() : DateTime.MinValue;
                var end = DateTime.TryParse(p.End, out var pe) ? pe.ToUniversalTime() : start;
                return (start, end);
            case FhirDateTime dt:
                var instant = DateTime.TryParse(dt.Value, out var parsed) ? parsed.ToUniversalTime() : DateTime.MinValue;
                return (instant, instant);
            default:
                return (DateTime.MinValue, DateTime.MaxValue);
        }
    }
}
