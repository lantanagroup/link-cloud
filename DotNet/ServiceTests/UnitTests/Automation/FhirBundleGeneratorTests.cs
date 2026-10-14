using LantanaGroup.Automation.Generation;
using LantanaGroup.Automation.Helpers;
using LantanaGroup.Link.Shared.Application.SerDes;
using System.Text.Json;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class FhirBundleGeneratorTests
{
    // ----------------------------------------------------------------------
    //  Core Generate(...) behavior
    // ----------------------------------------------------------------------

    [Fact]
    public void Generate_ReturnsRequestedPatientCountWithStableIds()
    {
        var output = new NullOutputHelper();

        var (patientIds, bundles) = FhirBundleGenerator.Generate(
            output, patientCount: 2, totalResourcesPerPatient: 120);

        Assert.Equal(2, patientIds.Count);

        // Patient IDs follow "Patient-{RunTag}-{ordinal:D3}". The ordinal is stable;
        // RunTag is a per-run 8-char hex tag that makes them collision-safe across
        // concurrent test runs.
        Assert.All(patientIds, id =>
        {
            var parts = id.Split('-');
            Assert.Equal(3, parts.Length);
            Assert.Equal("Patient", parts[0]);
            Assert.Equal(8, parts[1].Length);
        });
        Assert.EndsWith("-001", patientIds[0]);
        Assert.EndsWith("-002", patientIds[1]);
        Assert.NotEmpty(bundles);
    }

    [Fact]
    public void Generate_ProducesTransactionBundlesWithPutRequestsAndMax500Entries()
    {
        var output = new NullOutputHelper();

        var (_, bundles) = FhirBundleGenerator.Generate(
            output, patientCount: 1, totalResourcesPerPatient: 2500);

        Assert.True(bundles.Count > 1, "Expected multiple bundles when chunking above 500 entries.");

        foreach (var (name, json) in bundles)
        {
            Assert.Contains("_chunk", name);
            var bundle = ParseBundle(json);
            Assert.Equal(Bundle.BundleType.Transaction, bundle.Type);
            Assert.NotNull(bundle.Entry);
            Assert.True(bundle.Entry.Count <= 500, $"Bundle {name} has {bundle.Entry.Count} entries.");

            foreach (var entry in bundle.Entry)
            {
                Assert.NotNull(entry.Request);
                Assert.Equal(Bundle.HTTPVerb.PUT, entry.Request.Method);
                Assert.False(string.IsNullOrWhiteSpace(entry.Request.Url));
                Assert.NotNull(entry.Resource);
            }
        }
    }

    [Fact]
    public void Generate_FirstBundleContainsSharedInfrastructure()
    {
        var output = new NullOutputHelper();

        var (_, bundles) = FhirBundleGenerator.Generate(
            output, patientCount: 1, totalResourcesPerPatient: 100);
        var first = ParseBundle(bundles[0].Json);

        // Shared infrastructure IDs are run-tag-scoped: "{RunTag}-{Kind}".
        // Verify each expected kind exists by URL shape rather than by hardcoded legacy ids.
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Organization/") && e.Request.Url.EndsWith("-Org-Hospital"));
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Location/") && e.Request.Url.EndsWith("-Loc-Hospital"));
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Location/") && e.Request.Url.EndsWith("-Loc-ICU"));
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Location/") && e.Request.Url.EndsWith("-Loc-ED"));
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Location/") && e.Request.Url.EndsWith("-Loc-StepDown"));
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Location/") && e.Request.Url.EndsWith("-Loc-Outpatient"));
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Device/") && e.Request.Url.EndsWith("-Dev-PulseOx"));
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Device/") && e.Request.Url.EndsWith("-Dev-Ventilator"));
        Assert.Single(first.Entry, e => e.Request.Url!.StartsWith("Device/") && e.Request.Url.EndsWith("-Dev-CPAP"));
        Assert.Contains(first.Entry, e => e.Request.Url!.StartsWith("Practitioner/"));
    }

    [Fact]
    public void Generate_CreatesCoherentPrimaryDiagnosisAndEncounterLink()
    {
        var output = new NullOutputHelper();

        var (patientIds, bundles) = FhirBundleGenerator.Generate(
            output, patientCount: 1, totalResourcesPerPatient: 120);
        var patientId = patientIds.Single();
        var resources = FlattenResources(bundles);

        var encounter = resources.Values.OfType<Encounter>().Single(e => e.Id == $"{patientId}-Enc-001");
        Assert.NotNull(encounter.Diagnosis);
        Assert.NotEmpty(encounter.Diagnosis);

        var diagnosisRef = encounter.Diagnosis.First().Condition?.Reference;
        Assert.False(string.IsNullOrWhiteSpace(diagnosisRef));
        Assert.True(resources.ContainsKey(diagnosisRef!), $"Missing encounter diagnosis reference: {diagnosisRef}");

        var primaryCondition = Assert.IsType<Condition>(resources[diagnosisRef!]);
        Assert.Equal(patientId, primaryCondition.Subject?.Reference?.Split('/').Last());
        Assert.Equal($"Encounter/{patientId}-Enc-001", primaryCondition.Encounter?.Reference);
    }

    [Fact]
    public void Generate_SpecimenCollectorUsesPractitionerReference()
    {
        var output = new NullOutputHelper();

        var (_, bundles) = FhirBundleGenerator.Generate(
            output, patientCount: 1, totalResourcesPerPatient: 150);
        var resources = FlattenResources(bundles);

        var specimens = resources.Values.OfType<Specimen>().ToList();
        Assert.NotEmpty(specimens);

        foreach (var specimen in specimens)
        {
            var collectorRef = specimen.Collection?.Collector?.Reference;
            Assert.False(string.IsNullOrWhiteSpace(collectorRef));
            Assert.StartsWith("Practitioner/", collectorRef);
            Assert.True(resources.ContainsKey(collectorRef!), $"Collector reference not found: {collectorRef}");
        }
    }

    [Fact]
    public void Generate_ObservationAndDiagnosticReportReferencesResolve()
    {
        var output = new NullOutputHelper();

        var (_, bundles) = FhirBundleGenerator.Generate(
            output, patientCount: 1, totalResourcesPerPatient: 220);
        var resources = FlattenResources(bundles);

        foreach (var obs in resources.Values.OfType<Observation>())
        {
            var encounterRef = obs.Encounter?.Reference;
            Assert.False(string.IsNullOrWhiteSpace(encounterRef));
            Assert.True(resources.ContainsKey(encounterRef!));

            var subjectRef = obs.Subject?.Reference;
            Assert.False(string.IsNullOrWhiteSpace(subjectRef));
            Assert.True(resources.ContainsKey(subjectRef!));

            if (obs.Specimen != null)
                Assert.True(resources.ContainsKey(obs.Specimen.Reference!), $"Missing specimen reference: {obs.Specimen.Reference}");
        }

        foreach (var report in resources.Values.OfType<DiagnosticReport>())
        {
            foreach (var result in report.Result)
                Assert.True(resources.ContainsKey(result.Reference!), $"Missing report.result reference: {result.Reference}");

            foreach (var specimen in report.Specimen)
                Assert.True(resources.ContainsKey(specimen.Reference!), $"Missing report.specimen reference: {specimen.Reference}");
        }
    }

    [Fact]
    public void Generate_IsStructurallyDeterministicForSameInput()
    {
        // The generator is seed-deterministic for clinical content but uses a per-run
        // GUID tag for shared infrastructure IDs (collision safety across concurrent test
        // runs against the same FHIR server). Byte-for-byte JSON equality is therefore not
        // expected — but the *shape* of the output must be identical.
        var output = new NullOutputHelper();

        var run1 = FhirBundleGenerator.Generate(output, patientCount: 1, totalResourcesPerPatient: 180);
        var run2 = FhirBundleGenerator.Generate(output, patientCount: 1, totalResourcesPerPatient: 180);

        Assert.Equal(run1.PatientIds.Count, run2.PatientIds.Count);
        Assert.Equal(run1.Bundles.Count, run2.Bundles.Count);

        static Dictionary<string, int> ResourceTypeCounts(List<(string Name, string Json)> bundles)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in FlattenEntries(bundles))
            {
                var t = entry.Resource.TypeName;
                counts[t] = counts.TryGetValue(t, out var c) ? c + 1 : 1;
            }
            return counts;
        }

        var counts1 = ResourceTypeCounts(run1.Bundles);
        var counts2 = ResourceTypeCounts(run2.Bundles);
        Assert.Equal(counts1.OrderBy(k => k.Key), counts2.OrderBy(k => k.Key));
    }

    // ----------------------------------------------------------------------
    //  CQL filter simulator (data-driven, no generator coupling)
    // ----------------------------------------------------------------------

    [Fact]
    public void CqlFilterSimulator_AchProfile_ExcludesOnlyConditionsFailingSdeRules()
    {
        var encounterId = "TestPatient-Enc-001";
        var encStart = new DateTime(2024, 5, 10, 0, 0, 0, DateTimeKind.Utc);
        var encEnd = new DateTime(2024, 5, 15, 0, 0, 0, DateTimeKind.Utc);

        var includedProblemList = new CqlFilterSimulator.ConditionContext(
            "TestPatient-Condition-001",
            IsActive: true,
            RecordedDate: encStart.Date,             // strictly before encEnd.Date
            EncounterReference: $"Encounter/{encounterId}",
            CategoryCodes: ["problem-list-item"]);

        var excludedResolvedProblemList = includedProblemList with
        {
            ResourceId = "TestPatient-Condition-002",
            IsActive = false
        };

        var includedEncounterDiagnosis = new CqlFilterSimulator.ConditionContext(
            "TestPatient-Condition-003",
            IsActive: false, // status does not matter on this branch
            RecordedDate: encEnd.Date,
            EncounterReference: $"Encounter/{encounterId}",
            CategoryCodes: ["encounter-diagnosis"]);

        var excludedUnlinkedEncounterDiagnosis = includedEncounterDiagnosis with
        {
            ResourceId = "TestPatient-Condition-004",
            EncounterReference = "Encounter/OtherEnc"
        };

        var input = new CqlFilterSimulator.PatientCqlInput(
            PatientId: "TestPatient",
            EncounterId: encounterId,
            EncounterStart: encStart,
            EncounterEnd: encEnd,
            Conditions:
            [
                includedProblemList,
                excludedResolvedProblemList,
                includedEncounterDiagnosis,
                excludedUnlinkedEncounterDiagnosis
            ],
            Observations: []);

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation],
            input);

        Assert.Contains("Condition/TestPatient-Condition-001", excluded);
        Assert.Contains("Condition/TestPatient-Condition-002", excluded);
        Assert.Contains("Condition/TestPatient-Condition-004", excluded);
        Assert.DoesNotContain("Condition/TestPatient-Condition-003", excluded);
    }

    [Fact]
    public void CqlFilterSimulator_NoMeasuresOrNoProfiles_ReturnsEmpty()
    {
        var input = new CqlFilterSimulator.PatientCqlInput(
            "P", "P-Enc-001",
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc),
            [],
            []);

        Assert.Empty(CqlFilterSimulator.ComputeFilteredKeys(Array.Empty<ProfiledMeasureType>(), input));
    }

    [Fact]
    public void CqlFilterSimulator_MultiMeasure_IntersectsExclusions()
    {
        // MeasureEval evaluates each measure independently; PatientAggregator unions the
        // contained resources across the per-measure .mr files. So a resource is only truly
        // absent from ABS when EVERY applicable measure excludes it.
        //
        // ACH Monthly SDE Condition keeps only encounter-diagnosis linked to IP.
        // Hypoglycemic SDE Condition keeps conditions whose onset overlaps IP.
        // A resolved problem-list Condition recorded during the encounter is excluded by
        // ACH but included by Hypoglycemic -> must NOT appear in the excluded set.

        var encounterId = "P-Enc-001";
        var encStart = new DateTime(2024, 5, 10, 0, 0, 0, DateTimeKind.Utc);
        var encEnd = new DateTime(2024, 5, 15, 0, 0, 0, DateTimeKind.Utc);

        var resolvedProblemDuringEncounter = new CqlFilterSimulator.ConditionContext(
            "P-Condition-001",
            IsActive: false,
            RecordedDate: encStart.Date,
            EncounterReference: $"Encounter/{encounterId}",
            CategoryCodes: ["problem-list-item"]);

        // This one is excluded by BOTH profiles: resolved problem-list-item recorded AFTER
        // the encounter fails ACH (not active, date not strictly before end) AND fails
        // Hypoglycemic (recordedDate > encounterEnd).
        var resolvedProblemAfterEncounter = new CqlFilterSimulator.ConditionContext(
            "P-Condition-002",
            IsActive: false,
            RecordedDate: encEnd.Date.AddDays(5),
            EncounterReference: $"Encounter/{encounterId}",
            CategoryCodes: ["problem-list-item"]);

        var input = new CqlFilterSimulator.PatientCqlInput(
            PatientId: "P",
            EncounterId: encounterId,
            EncounterStart: encStart,
            EncounterEnd: encEnd,
            Conditions: [resolvedProblemDuringEncounter, resolvedProblemAfterEncounter],
            Observations: []);

        var excluded = CqlFilterSimulator.ComputeFilteredKeys(
            [
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
            ],
            input);

        // Only excluded if BOTH measures exclude it.
        Assert.DoesNotContain("Condition/P-Condition-001", excluded);
        Assert.Contains("Condition/P-Condition-002", excluded);
    }


    // ----------------------------------------------------------------------
    //  Helpers
    // ----------------------------------------------------------------------

    private static Bundle ParseBundle(string json)
    {
        var bundle = JsonSerializer.Deserialize<Bundle>(json, LinkFhirSerializerOptions.ForFhirWithoutValidation());
        Assert.NotNull(bundle);
        return bundle!;
    }

    private static Dictionary<string, Resource> FlattenResources(List<(string Name, string Json)> bundles)
    {
        var map = new Dictionary<string, Resource>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, json) in bundles)
        {
            var bundle = ParseBundle(json);
            foreach (var entry in bundle.Entry)
            {
                var key = entry.Request.Url!; // resourceType/id for PUT transaction entries
                map[key] = entry.Resource;
            }
        }

        return map;
    }

    private static List<Bundle.EntryComponent> FlattenEntries(List<(string Name, string Json)> bundles)
    {
        var entries = new List<Bundle.EntryComponent>();

        foreach (var (_, json) in bundles)
        {
            var bundle = ParseBundle(json);
            Assert.NotNull(bundle.Entry);
            entries.AddRange(bundle.Entry);
        }

        return entries;
    }


    private sealed class NullOutputHelper : IAutomationOutput
    {
        public void WriteLine(string message) { }
        public void WriteLine(string format, params object[] args) { }
    }
}
