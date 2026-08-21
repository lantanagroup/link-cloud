using System.Text.Json;
using Hl7.Fhir.Model;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Automation.Generation.Thetis;
using LantanaGroup.Automation.Helpers;
using LantanaGroup.Link.Shared.Application.SerDes;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Automation;

/// <summary>
/// A3: Thetis-path invariants. Classic factory tests stay in
/// <see cref="FhirBundleGeneratorTests"/>. Do not bitwise-compare generators.
/// </summary>
[Trait("Category", "UnitTests")]
public class ThetisGenerationInvariantTests
{
    private const int FrozenSeed = 20260326;
    private const int ResourcesPerPatient = 50;
    private const string RunTag = "abcd1234";
    private const string PeriodStart = "2026-01-01T00:00:00Z";
    private const string PeriodEnd = "2026-01-31T23:59:59Z";
    private static readonly DateTime PeriodStartUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEndUtc = new(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc);
    private static readonly HashSet<string> InpatientClasses = new(StringComparer.OrdinalIgnoreCase) { "IMP", "SS", "AC" };

    private static readonly IReadOnlyList<ProfiledMeasureType> AchAndHypo =
    [
        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
        ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
    ];

    [Fact]
    public void UseThetisEngine_defaults_true()
    {
        Assert.True(new FhirGenerationConfig().UseThetisEngine);
    }

    [Fact]
    public async Task Thetis_two_patients_satisfy_deprecation_invariants()
    {
        var (ids, shared, patients) = await GenerateThetisPatientsAsync();

        Assert.Equal(2, patients.Count);

        foreach (var generated in patients)
        {
            AssertPatientEncounterAndAnchors(generated, ids.RunTag);
            AssertEncounterInsideReportWindow(generated.Encounter);
            Assert.Contains(generated.Encounter.Class?.Code, InpatientClasses);
            AssertAllIdsEmbedRunTag(generated.Entries, ids.RunTag);
        }

        var hypo = patients[0];
        Assert.True(hypo.Profile.RequiresHypoglycemicMedication());
        Assert.Contains(hypo.Entries.Select(e => e.Resource), r => r is MedicationRequest mr && IsInsulin(mr, ids.HypoInsulinGlargineMedication));

        var achOnly = patients[1];
        Assert.True(achOnly.Profile.RequiresInpatientEncounter());
        Assert.False(achOnly.Profile.RequiresHypoglycemicMedication());

        AssertManifestPredictionMatchesSimulator(patients, shared);
    }

    [Fact]
    public async Task Thetis_same_seed_repeats_anchor_ids()
    {
        var first = await GenerateThetisPatientsAsync();
        var second = await GenerateThetisPatientsAsync();

        Assert.Equal(
            first.Patients.SelectMany(p => AnchorIds(p)).ToArray(),
            second.Patients.SelectMany(p => AnchorIds(p)).ToArray());
    }

    [Fact]
    public async Task Classic_and_Thetis_share_anchor_invariants_not_json()
    {
        var (ids, _, thetisPatients) = await GenerateThetisPatientsAsync();
        var thetis = thetisPatients[1];

        var classic = FhirBundleGenerator.GeneratePatientEntries(
            thetis.PatientId,
            ids,
            thetis.Request.SharedPractitionerIds,
            thetis.Request.SharedMedicationIds,
            ResourcesPerPatient,
            FrozenSeed + 1,
            new FhirGenerationConfig { UseThetisEngine = false },
            clinicalPeriodStart: PeriodStartUtc,
            clinicalPeriodEnd: PeriodEndUtc);

        Assert.Contains(classic, e => e.Resource is Patient);
        Assert.Contains(classic, e => e.Resource is Encounter);
        Assert.Contains(thetis.Entries, e => e.Resource is Patient);
        Assert.Contains(thetis.Entries, e => e.Resource is Encounter);

        var options = LinkFhirSerializerOptions.ForFhirWithoutValidation();
        var classicJson = JsonSerializer.Serialize(classic.Single(e => e.Resource is Patient).Resource, options);
        var thetisJson = JsonSerializer.Serialize(thetis.Entries.Single(e => e.Resource is Patient).Resource, options);
        Assert.NotEqual(classicJson, thetisJson);
    }

    [Fact]
    public async Task PatientId_override_is_honored_by_both_generators()
    {
        const string overrideId = "mock-patient-0007";
        var (ids, _, practitionerIds, medicationIds) =
            FactorySharedInfrastructureGenerator.Shared.Generate(null, "deadbeef");

        var request = new PatientEntryRequest
        {
            Profile = new PatientProfile(new Dictionary<ProfiledMeasureType, MeasureEligibility>
            {
                [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying
            }),
            PatientIndex = 0,
            BaseSeed = 11,
            TotalResourcesPerPatient = 20,
            SharedPractitionerIds = practitionerIds,
            SharedMedicationIds = medicationIds,
            Measures = [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation],
            Ids = ids,
            PatientId = overrideId
        };

        var thetis = await ThetisPatientEntryGenerator.Shared.GenerateAsync(request);
        var classic = await ClassicPatientEntryGenerator.Shared.GenerateAsync(request);

        Assert.Equal(overrideId, Assert.Single(thetis.Select(e => e.Resource).OfType<Patient>()).Id);
        Assert.Equal(overrideId, Assert.Single(classic.Select(e => e.Resource).OfType<Patient>()).Id);
        Assert.Contains(thetis, e => e.Resource is Encounter enc && enc.Id == $"{overrideId}-Enc-001");
        Assert.Contains(classic, e => e.Resource is Encounter enc && enc.Id == $"{overrideId}-Enc-001");
    }

    private static async Task<(
        FhirBundleGenerator.SharedIds Ids,
        List<Bundle.EntryComponent> Shared,
        List<GeneratedPatient> Patients)> GenerateThetisPatientsAsync()
    {
        var (ids, shared, practitionerIds, medicationIds) =
            FactorySharedInfrastructureGenerator.Shared.Generate(null, RunTag);

        var hypo = new PatientProfile(
            new Dictionary<ProfiledMeasureType, MeasureEligibility>
            {
                [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying,
                [ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation] = MeasureEligibility.Qualifying
            },
            SeedOffset: 0,
            ClinicalScenarioId: ClinicalScenarioIds.DiabeticHypoglycemia.ToString());

        var achOnly = new PatientProfile(
            new Dictionary<ProfiledMeasureType, MeasureEligibility>
            {
                [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying,
                [ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation] = MeasureEligibility.NonQualifying
            },
            SeedOffset: 1,
            ClinicalScenarioId: ClinicalScenarioIds.Pneumonia.ToString());

        var patients = new List<GeneratedPatient>();
        foreach (var (profile, index) in new[] { (hypo, 0), (achOnly, 1) })
        {
            var request = new PatientEntryRequest
            {
                Profile = profile,
                PatientIndex = index,
                BaseSeed = FrozenSeed,
                TotalResourcesPerPatient = ResourcesPerPatient,
                SharedPractitionerIds = practitionerIds,
                SharedMedicationIds = medicationIds,
                Measures = AchAndHypo,
                ClinicalPeriodStart = PeriodStartUtc,
                ClinicalPeriodEnd = PeriodEndUtc,
                Config = new FhirGenerationConfig { UseThetisEngine = true },
                Ids = ids,
                Output = new NullOutputHelper()
            };

            var entries = await ThetisPatientEntryGenerator.Shared.GenerateAsync(request);
            patients.Add(new GeneratedPatient(ids.PatientId(index), profile, request, entries));
        }

        return (ids, shared, patients);
    }

    private static void AssertPatientEncounterAndAnchors(GeneratedPatient generated, string runTag)
    {
        var patient = Assert.Single(generated.Entries.Select(e => e.Resource).OfType<Patient>());
        Assert.Equal(generated.PatientId, patient.Id);
        Assert.Contains(runTag, patient.Id, StringComparison.Ordinal);

        Assert.NotNull(generated.Encounter);
        Assert.Equal($"{generated.PatientId}-Enc-001", generated.Encounter.Id);

        Assert.Contains(generated.Entries, e => e.Resource is Device d && d.Id == $"{generated.PatientId}-Device-001");
        Assert.Contains(generated.Entries, e => e.Resource is CareTeam);
        Assert.Contains(generated.Entries, e => e.Resource is CarePlan);
        Assert.Contains(generated.Entries, e => e.Resource is Hl7.Fhir.Model.List);
    }

    private static void AssertEncounterInsideReportWindow(Encounter encounter)
    {
        Assert.False(string.IsNullOrWhiteSpace(encounter.Period?.Start));
        Assert.False(string.IsNullOrWhiteSpace(encounter.Period?.End));
        var start = DateTimeOffset.Parse(encounter.Period!.Start).UtcDateTime;
        var end = DateTimeOffset.Parse(encounter.Period.End).UtcDateTime;
        Assert.True(start < end, "encounter period must be ordered");
        Assert.True(end > PeriodStartUtc, "encounter must overlap the report window");
        Assert.True(start < PeriodEndUtc, "encounter must overlap the report window");
    }

    private static void AssertAllIdsEmbedRunTag(IEnumerable<Bundle.EntryComponent> entries, string runTag)
    {
        foreach (var entry in entries)
        {
            var id = entry.Resource?.Id;
            Assert.False(string.IsNullOrWhiteSpace(id), $"{entry.Resource?.TypeName} is missing Id");
            Assert.Contains(runTag, id!, StringComparison.Ordinal);
        }
    }

    private static void AssertManifestPredictionMatchesSimulator(
        List<GeneratedPatient> patients,
        List<Bundle.EntryComponent> shared)
    {
        var plan = EncounterAndObservationPlan();
        var sharedIndex = ToResourceIndex(shared);
        var builder = new GenerationManifest.IncrementalBuilder();
        builder.AddEntries(string.Empty, shared);

        foreach (var generated in patients)
        {
            var patientIndex = ToResourceIndex(generated.Entries);
            var acquired = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
                generated.PatientId,
                patientIndex,
                sharedIndex,
                plan,
                PeriodStart,
                PeriodEnd);

            var again = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
                generated.PatientId,
                patientIndex,
                sharedIndex,
                plan,
                PeriodStart,
                PeriodEnd);

            Assert.True(acquired.SetEquals(again), "simulator must be deterministic for a frozen Thetis patient");

            builder.AddPatient(generated.PatientId, generated.Profile);
            builder.AddEntries(generated.PatientId, generated.Entries);
            builder.SetSimulatedAcquiredKeys(generated.PatientId, acquired);
        }

        var manifest = builder.Build(AchAndHypo);
        foreach (var generated in patients)
        {
            var expected = manifest.GetExpectedAbsKeysForPatient(generated.PatientId);
            Assert.Contains($"Patient/{generated.PatientId}", expected, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(expected, k => k.StartsWith("Encounter/", StringComparison.OrdinalIgnoreCase));

            var simulated = manifest.SimulatedAcquiredResourceKeysByPatient[generated.PatientId];
            foreach (var key in simulated.Where(k =>
                         k.StartsWith("Encounter/", StringComparison.OrdinalIgnoreCase)
                         || k.StartsWith("Observation/", StringComparison.OrdinalIgnoreCase)))
            {
                Assert.Contains(key, expected, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static QueryPlanInput EncounterAndObservationPlan() => new()
    {
        EhrDescription = "Thetis invariant",
        LookBack = "P0D",
        InitialQueries =
        [
            new QueryPlanQueryEntry
            {
                ResourceType = "Encounter",
                QueryConfigType = "Parameter",
                Parameters =
                [
                    new QueryPlanParameterEntry { ParameterType = "Variable", Name = "patient", Variable = 0 },
                    new QueryPlanParameterEntry { ParameterType = "Variable", Name = "date", Variable = 2, Format = "ge{0}" },
                    new QueryPlanParameterEntry { ParameterType = "Variable", Name = "date", Variable = 3, Format = "le{0}" }
                ]
            }
        ],
        SupplementalQueries =
        [
            new QueryPlanQueryEntry
            {
                ResourceType = "Observation",
                QueryConfigType = "Parameter",
                Parameters =
                [
                    new QueryPlanParameterEntry { ParameterType = "Variable", Name = "patient", Variable = 0 },
                    new QueryPlanParameterEntry { ParameterType = "Variable", Name = "date", Variable = 2, Format = "ge{0}" },
                    new QueryPlanParameterEntry { ParameterType = "Variable", Name = "date", Variable = 3, Format = "le{0}" }
                ]
            }
        ]
    };

    private static List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)> ToResourceIndex(
        IReadOnlyList<Bundle.EntryComponent> entries)
    {
        var options = LinkFhirSerializerOptions.ForFhirWithoutValidation();
        var result = new List<(string, string, string, JsonElement)>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Resource is null || string.IsNullOrWhiteSpace(entry.Resource.Id))
                continue;
            var json = JsonSerializer.Serialize(entry.Resource, options);
            using var doc = JsonDocument.Parse(json);
            result.Add((entry.Resource.TypeName, entry.Resource.Id, $"{entry.Resource.TypeName}/{entry.Resource.Id}", doc.RootElement.Clone()));
        }

        return result;
    }

    private static bool IsInsulin(MedicationRequest request, string hypoMedicationId)
    {
        switch (request.Medication)
        {
            case CodeableConcept concept:
                return concept.Coding.Any(c => c.Code == "274783");
            case ResourceReference reference:
                return (!string.IsNullOrWhiteSpace(hypoMedicationId)
                        && reference.Reference?.Contains(hypoMedicationId, StringComparison.OrdinalIgnoreCase) == true)
                       || (reference.Display?.Contains("insulin", StringComparison.OrdinalIgnoreCase) ?? false);
            default:
                return request.Contained?.OfType<Medication>().Any(m =>
                    m.Code?.Coding?.Any(c => c.Code == "274783") == true) == true;
        }
    }

    private static IEnumerable<string> AnchorIds(GeneratedPatient generated)
    {
        yield return generated.PatientId;
        yield return generated.Encounter.Id;
        yield return generated.Entries.Select(e => e.Resource).OfType<Condition>().First().Id!;
        yield return generated.Entries.Select(e => e.Resource).OfType<Device>().First(d => d.Id == $"{generated.PatientId}-Device-001").Id!;
        yield return generated.Entries.Select(e => e.Resource).OfType<CareTeam>().First().Id!;
        yield return generated.Entries.Select(e => e.Resource).OfType<CarePlan>().First().Id!;
        yield return generated.Entries.Select(e => e.Resource).OfType<Hl7.Fhir.Model.List>().First().Id!;
    }

    private sealed record GeneratedPatient(
        string PatientId,
        PatientProfile Profile,
        PatientEntryRequest Request,
        List<Bundle.EntryComponent> Entries)
    {
        public Encounter Encounter => Entries.Select(e => e.Resource).OfType<Encounter>().Single();
    }

    private sealed class NullOutputHelper : IAutomationOutput
    {
        public void WriteLine(string message) { }
        public void WriteLine(string format, params object[] args) { }
    }
}
