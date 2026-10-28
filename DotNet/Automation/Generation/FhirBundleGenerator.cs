using Hl7.Fhir.Model;
using LantanaGroup.Automation.Generation.Thetis;
using LantanaGroup.Automation.Helpers;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Shared generation IDs, encounter-window helpers, and a small-dataset Thetis
/// convenience that chunks transaction bundles. Production runs go through
/// <see cref="FhirGenerationPipeline"/>. Per-patient clinical synthesis is Thetis Engine;
/// shared org/location/practitioner/medication resources stay factory-built.
/// </summary>
public static class FhirBundleGenerator
{
    public const int DefaultPatientCount = 1;
    public const int DefaultResourcesPerPatient = 5_000;
    private const int MaxEntriesPerBundle = 500;

    // Legacy shared infrastructure IDs - kept for backward compatibility / reference.
    // New runs should use SharedIds, which derives all IDs from a per-run RunTag.
    public const string HospitalLocationId = "Gen-Location-Hospital";
    public const string IcuLocationId = "Gen-Location-ICU";
    public const string EdLocationId = "Gen-Location-ED";
    public const string StepDownLocationId = "Gen-Location-StepDown";
    public const string OutpatientLocationId = "Gen-Location-Outpatient";
    public const string HospitalOrgId = "Gen-Org-Hospital";

    /// <summary>
    /// Shared Medication for the insulin glargine concept (RxNorm 274783) used by
    /// the Hypoglycemic measure value set. This is distinct from the formulary entry
    /// at index 8 (RxNorm 1116635) which is a different clinical drug concept.
    /// </summary>
    public const string HypoInsulinGlargineMedicationId = "Gen-Medication-HypoInsulinGlargine";

    /// <summary>
    /// Run-scoped shared infrastructure IDs. A short GUID tag (RunTag) ensures concurrent
    /// test runs against the same FHIR server don't conflict on infrastructure resources.
    /// </summary>
    public sealed record SharedIds(string? RunTagOverride = null)
    {
        /// <summary>Short unique tag for this generation run (8 hex chars from a GUID).</summary>
        public string RunTag { get; } = string.IsNullOrWhiteSpace(RunTagOverride)
            ? Guid.NewGuid().ToString("N")[..8]
            : RunTagOverride.Trim();

        public string HospitalLocation => $"{RunTag}-Loc-Hospital";
        public string IcuLocation => $"{RunTag}-Loc-ICU";
        public string EdLocation => $"{RunTag}-Loc-ED";
        public string StepDownLocation => $"{RunTag}-Loc-StepDown";
        public string OutpatientLocation => $"{RunTag}-Loc-Outpatient";
        public string Organization => $"{RunTag}-Org-Hospital";
        public string HypoInsulinGlargineMedication => $"{RunTag}-Med-HypoInsulinGlargine";
        public string DevicePulseOx => $"{RunTag}-Dev-PulseOx";
        public string DeviceVentilator => $"{RunTag}-Dev-Ventilator";
        public string DeviceCPAP => $"{RunTag}-Dev-CPAP";
        public string MedicationId(int index) => $"{RunTag}-Med-{index + 1:D3}";

        /// <summary>
        /// Returns a stable unique patient ID for the given index. RunTag scopes the run,
        /// the ordinal segment is the patient index.
        /// </summary>
        public string PatientId(int index) => $"Patient-{RunTag}-{index + 1:D3}";

        /// <summary>Generate a unique practitioner ID scoped to this run.</summary>
        public string PractitionerId(int index) => $"{RunTag}-Pract-{index + 1:D3}";
    }

    /// <summary>
    /// Abbreviates a FHIR resource type name for use in generated resource IDs.
    /// Keeps IDs under the FHIR 64-character limit.
    /// </summary>
    internal static string AbbreviateResourceType(string resourceType) => resourceType switch
    {
        "MedicationAdministration" => "MedAdm",
        "MedicationRequest" => "MedReq",
        "DiagnosticReport" => "DxRpt",
        "ServiceRequest" => "SvcReq",
        "AllergyIntolerance" => "AlgInt",
        "ImagingStudy" => "ImgSty",
        "DocumentReference" => "DocRef",
        "MeasureReport" => "MR",
        "OperationOutcome" => "OO",
        _ => resourceType
    };

    public static (List<string> PatientIds, List<(string Name, string Json)> Bundles) Generate(
        IAutomationOutput output,
        int patientCount = DefaultPatientCount,
        int totalResourcesPerPatient = DefaultResourcesPerPatient,
        int? generationSeed = null,
        FhirGenerationConfig? config = null,
        GenerationRequirementsPlan? generationRequirementsPlan = null,
        DateTime? clinicalPeriodStart = null,
        DateTime? clinicalPeriodEnd = null)
    {
        var patientIds = new List<string>();
        var allEntries = new List<(string PatientId, List<Bundle.EntryComponent> Entries)>();
        var baseSeed = generationSeed.GetValueOrDefault();
        var ids = new SharedIds();

        output.WriteLine($"Generating {patientCount} patients with ~{totalResourcesPerPatient} resources each..." +
                         (generationSeed.HasValue ? $" (seed={generationSeed.Value})" : string.Empty));

        // ------------------------------------------------------------------
        // Shared infrastructure - uploaded once in the first chunk
        // ------------------------------------------------------------------
        var (sharedEntries, sharedPractitionerIds, sharedMedicationIds) =
            ScenarioResourceGeneration.BuildSharedInfrastructure(ids, generationRequirementsPlan);

        var profile = new PatientProfile(new Dictionary<ProfiledMeasureType, MeasureEligibility>
        {
            [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying
        });
        IReadOnlyList<ProfiledMeasureType> measures =
            [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation];

        // ------------------------------------------------------------------
        // Per-patient generation (Thetis)
        // ------------------------------------------------------------------
        for (var p = 0; p < patientCount; p++)
        {
            var patientId = ids.PatientId(p);
            patientIds.Add(patientId);

            var entries = ThetisPatientEntryGenerator.Shared.GenerateAsync(new PatientEntryRequest
            {
                Profile = profile,
                PatientIndex = p,
                BaseSeed = baseSeed,
                TotalResourcesPerPatient = totalResourcesPerPatient,
                SharedPractitionerIds = sharedPractitionerIds,
                SharedMedicationIds = sharedMedicationIds,
                Measures = measures,
                ClinicalPeriodStart = clinicalPeriodStart,
                ClinicalPeriodEnd = clinicalPeriodEnd,
                Config = config,
                RequirementsPlan = generationRequirementsPlan,
                Ids = ids,
                Output = output
            }).GetAwaiter().GetResult();

            output.WriteLine($"  Patient {patientId}: {entries.Count} Thetis entries");
            allEntries.Add((patientId, entries));
        }

        // ------------------------------------------------------------------
        // Chunk into transaction bundles
        // ------------------------------------------------------------------
        var bundles = new List<(string Name, string Json)>();
        var currentChunk = new List<Bundle.EntryComponent>(sharedEntries);
        var currentPatientId = "shared";
        var chunkIndex = 0;

        foreach (var (patientId, entries) in allEntries)
        {
            currentPatientId = patientId;
            foreach (var entry in entries)
            {
                currentChunk.Add(entry);
                if (currentChunk.Count >= MaxEntriesPerBundle)
                {
                    chunkIndex++;
                    bundles.Add(($"{currentPatientId}_chunk{chunkIndex:D2}", ScenarioResourceGeneration.Serialize(currentChunk)));
                    currentChunk = [];
                }
            }
        }

        if (currentChunk.Count > 0)
        {
            chunkIndex++;
            bundles.Add(($"{currentPatientId}_chunk{chunkIndex:D2}", ScenarioResourceGeneration.Serialize(currentChunk)));
        }

        output.WriteLine($"Generated {bundles.Count} transaction bundles for {patientCount} patients.");
        return (patientIds, bundles);
    }


    internal static DateTime EncounterStart(int index)
    {
        const int baseYear = 2023;
        const int baseMonth = 1;
        var monthOffset = index % 12;
        var dayOffset = (index * 3) % 28;
        var month = ((baseMonth - 1 + monthOffset) % 12) + 1;
        var year = baseYear + (baseMonth - 1 + monthOffset) / 12;
        return new DateTime(year, month, 1 + dayOffset, 6 + (index % 6), 0, 0, DateTimeKind.Utc);
    }

    internal static DateTime EncounterEnd(int index) =>
        EncounterStart(index).AddDays(2 + ((index * 7) % 20)).AddHours(4);

    /// <summary>
    /// Returns a deterministic <c>(start, end)</c> inpatient encounter window for
    /// the given patient seed, optionally bounded inside a caller-supplied clinical
    /// period.
    ///
    /// When <paramref name="clinicalPeriodStart"/> and <paramref name="clinicalPeriodEnd"/>
    /// are both supplied, the window is placed entirely inside that range so every
    /// resource derived from the encounter (Observations spread across the LOS,
    /// Conditions onset on admission, MedicationRequests, etc.) falls inside the
    /// period and is therefore eligible to be picked up by any downstream consumer
    /// that filters on FHIR <c>date=ge…&amp;date=le…</c> search parameters. Without
    /// this bound the seed-only scheme can produce encounters whose tail spills past
    /// the period end, and a date-filtering consumer would silently drop late-encounter
    /// resources while the generation simulator still counted them as expected — the
    /// asymmetric loss that surfaces as "actual &lt; expected" in downstream
    /// reconciliation (e.g. <c>ReportAbsManifestValidator</c> in the Link consumer).
    ///
    /// The Automation library treats this as a generic FHIR-system concept: any
    /// caller that wants generated data anchored to a specific time window can pass
    /// one, regardless of whether that window represents a Link reporting period, a
    /// study window, an audit period, or anything else.
    ///
    /// Determinism contract: same <paramref name="seed"/> + same period ⇒ same
    /// dates on every invocation. Same seed + different period ⇒ different dates
    /// (necessarily, since the encounter is bound to a different range). This
    /// matches the project's "seed + config reproduces a run" guarantee.
    ///
    /// Falls back to the seed-only <see cref="EncounterStart"/>/<see cref="EncounterEnd"/>
    /// scheme when the period is null/empty/inverted, so existing callers that
    /// don't supply a window keep their stable 2023-anchored dates.
    /// </summary>
    internal static (DateTime Start, DateTime End) DeriveInpatientEncounterWindow(
        int seed, DateTime? clinicalPeriodStart, DateTime? clinicalPeriodEnd)
    {
        if (!clinicalPeriodStart.HasValue || !clinicalPeriodEnd.HasValue
            || clinicalPeriodEnd.Value <= clinicalPeriodStart.Value)
        {
            return (EncounterStart(seed), EncounterEnd(seed));
        }

        var rs = DateTime.SpecifyKind(clinicalPeriodStart.Value, DateTimeKind.Utc);
        var re = DateTime.SpecifyKind(clinicalPeriodEnd.Value, DateTimeKind.Utc);
        var periodMinutes = (long)(re - rs).TotalMinutes;
        if (periodMinutes <= 0)
            return (EncounterStart(seed), EncounterEnd(seed));

        // Same LOS distribution as EncounterEnd(EncounterStart(seed)) — 2-22 days
        // plus 4 hours — so the *shape* of the encounter (and therefore the
        // resource-density pattern of GenerateScenarioDrivenResources) is preserved
        // when a bound is added. Clamp to the period (minus a 1-hour cushion at
        // each end) so encStart and encEnd always lie strictly inside the window.
        var losDays = 2 + Mod(seed * 7, 20);
        var losMinutes = (long)losDays * 24 * 60 + 4 * 60;
        var maxLos = Math.Max(60, periodMinutes - 60);
        if (losMinutes > maxLos) losMinutes = maxLos;

        // Deterministic placement: spread starts across the available window via a
        // co-prime multiplier so small cohorts (5-20 patients) don't all cluster
        // at the same offset. Math.Abs guards against negative seeds (defensive —
        // the pipeline only uses non-negative seeds today).
        var startRange = Math.Max(1, periodMinutes - losMinutes);
        var offsetMinutes = Math.Abs((long)seed * 1009 + 7919) % startRange;

        var encStart = rs.AddMinutes(offsetMinutes);
        var encEnd = encStart.AddMinutes(losMinutes);

        // Defensive boundary clamp — losMinutes was already capped, this just
        // covers floating-point edge cases at the period end.
        if (encEnd > re) encEnd = re;
        return (encStart, encEnd);
    }

    /// <summary>
    /// Deterministic <c>(start, end)</c> for outpatient (ambulatory) encounters,
    /// optionally bounded by a caller-supplied clinical period. Matches the same
    /// contract as <see cref="DeriveInpatientEncounterWindow"/>: seed + period ⇒
    /// stable dates.
    ///
    /// Outpatient LOS is hours, not days, so the encounter virtually always fits
    /// inside a monthly/daily window — the bounding logic is here mainly for
    /// symmetry with the inpatient case and to avoid edge cases on very short
    /// (e.g. single-day) periods. When no period is supplied, falls back to the
    /// legacy 2020-anchored ambulatory scheme used by
    /// <c>FhirGenerationPipeline.GeneratePatientEntries</c>.
    /// </summary>
    internal static (DateTime Start, DateTime End) DeriveOutpatientEncounterWindow(
        int seed, DateTime? clinicalPeriodStart, DateTime? clinicalPeriodEnd)
    {
        if (!clinicalPeriodStart.HasValue || !clinicalPeriodEnd.HasValue
            || clinicalPeriodEnd.Value <= clinicalPeriodStart.Value)
        {
            // Legacy 2020-anchored scheme — preserved bit-for-bit so existing
            // tests that don't pass a period get identical dates.
            var legacyStart = new DateTime(2020, 1 + Mod(seed, 6), 1 + Mod(seed * 3, 28),
                                           8 + Mod(seed, 4), 0, 0, DateTimeKind.Utc);
            return (legacyStart, legacyStart.AddHours(2 + Mod(seed, 4)));
        }

        var rs = DateTime.SpecifyKind(clinicalPeriodStart.Value, DateTimeKind.Utc);
        var re = DateTime.SpecifyKind(clinicalPeriodEnd.Value, DateTimeKind.Utc);
        var periodMinutes = (long)(re - rs).TotalMinutes;
        if (periodMinutes <= 0)
        {
            var legacyStart = new DateTime(2020, 1 + Mod(seed, 6), 1 + Mod(seed * 3, 28),
                                           8 + Mod(seed, 4), 0, 0, DateTimeKind.Utc);
            return (legacyStart, legacyStart.AddHours(2 + Mod(seed, 4)));
        }

        var losMinutes = (long)(2 + Mod(seed, 4)) * 60;
        if (losMinutes >= periodMinutes) losMinutes = Math.Max(60, periodMinutes - 60);

        var startRange = Math.Max(1, periodMinutes - losMinutes);
        // Different multiplier than the inpatient helper so the two schemes don't
        // collide for the same seed (e.g. mixed-eligibility cohorts where one
        // patient flips inpatient↔outpatient between runs would otherwise land
        // on identical offsets and look suspiciously synchronised in the logs).
        var offsetMinutes = Math.Abs((long)seed * 1013 + 4001) % startRange;

        var encStart = rs.AddMinutes(offsetMinutes);
        var encEnd = encStart.AddMinutes(losMinutes);
        if (encEnd > re) encEnd = re;
        return (encStart, encEnd);
    }

    /// <summary>
    /// Builds the run-scoped shared FHIR infrastructure (Organization, Locations, Devices,
    /// Practitioners, Medications) that all per-patient resources reference.
    /// Call once per server lifetime and reuse the returned values in every
    /// Thetis patient generation request.
    /// </summary>
    public static (SharedIds Ids, List<Bundle.EntryComponent> SharedEntries, List<string> PractitionerIds, List<string> MedicationIds)
        BuildSharedResources()
    {
        var ids = new SharedIds();
        var (sharedEntries, practitionerIds, medicationIds) = ScenarioResourceGeneration.BuildSharedInfrastructure(ids);
        return (ids, sharedEntries, practitionerIds, medicationIds);
    }

    internal static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;
}
