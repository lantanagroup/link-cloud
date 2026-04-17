using Hl7.Fhir.Model;
using LantanaGroup.Automation.Generation.ResourceFactories;
using LantanaGroup.Automation.Helpers;
using System.Text.Json;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Orchestrates synthetic FHIR R4 transaction bundle generation for E2E / stress / volume tests.
///
/// Every patient is assigned a <see cref="FhirGenerationCodes.ClinicalScenarios"/> row that drives
/// the primary diagnosis, admission type, medication choices, and procedure reasons so the full
/// set of resources for a patient forms a coherent clinical story.
///
/// All resource creation is delegated to the per-resource *Factory classes in this namespace.
/// Bundles are chunked to stay within FHIR server transaction size limits (500 entries).
/// </summary>
public static class FhirBundleGenerator
{
    public const int DefaultPatientCount = 1;
    public const int DefaultResourcesPerPatient = 5_000;
    private const int MaxEntriesPerBundle = 500;

    // Legacy shared infrastructure IDs — kept for backward compatibility / reference.
    // New runs should use SharedIds derived from the patient prefix.
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
    /// Prefix-scoped shared infrastructure IDs. A short GUID tag ensures concurrent
    /// test runs against the same FHIR server don't conflict on infrastructure resources.
    /// </summary>
    public sealed record SharedIds(string Prefix)
    {
        /// <summary>Short unique tag for this generation run (8 hex chars from a GUID).</summary>
        public string RunTag { get; } = Guid.NewGuid().ToString("N")[..8];

        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> _patientIds = new();

        public string HospitalLocation => $"{Prefix}-{RunTag}-Loc-Hospital";
        public string IcuLocation => $"{Prefix}-{RunTag}-Loc-ICU";
        public string EdLocation => $"{Prefix}-{RunTag}-Loc-ED";
        public string StepDownLocation => $"{Prefix}-{RunTag}-Loc-StepDown";
        public string OutpatientLocation => $"{Prefix}-{RunTag}-Loc-Outpatient";
        public string Organization => $"{Prefix}-{RunTag}-Org-Hospital";
        public string HypoInsulinGlargineMedication => $"{Prefix}-{RunTag}-Med-HypoInsulinGlargine";
        public string DevicePulseOx => $"{Prefix}-{RunTag}-Dev-PulseOx";
        public string DeviceVentilator => $"{Prefix}-{RunTag}-Dev-Ventilator";
        public string DeviceCPAP => $"{Prefix}-{RunTag}-Dev-CPAP";
        public string MedicationId(int index) => $"{Prefix}-{RunTag}-Med-{index + 1:D3}";

        /// <summary>
        /// Returns a stable unique patient ID for the given index.
        /// The GUID component is generated once per index and cached.
        /// </summary>
        public string PatientId(int index) =>
            _patientIds.GetOrAdd(index, i => $"{Prefix}-{i + 1:D3}-{Guid.NewGuid().ToString("N")[..8]}");

        /// <summary>Generate a unique practitioner ID scoped to this run.</summary>
        public string PractitionerId(int index) => $"{Prefix}-{RunTag}-Pract-{index + 1:D3}";
    }

    private static (string ResourceType, double Fraction)[] ResolveDistribution(FhirGenerationConfig? config)
    {
        var dict = (config ?? new FhirGenerationConfig()).ResourceDistribution;
        return dict.Select(kv => (kv.Key, kv.Value)).ToArray();
    }

    /// <summary>
    /// Abbreviates a FHIR resource type name for use in generated resource IDs.
    /// Keeps IDs under the FHIR 64-character limit even with longer patient ID prefixes.
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
        string patientIdPrefix = "MegaPatient",
        int? generationSeed = null,
        FhirGenerationConfig? config = null)
    {
        var patientIds = new List<string>();
        var allEntries = new List<(string PatientId, List<Bundle.EntryComponent> Entries)>();
        var baseSeed = generationSeed.GetValueOrDefault();
        var ids = new SharedIds(patientIdPrefix);

        output.WriteLine($"Generating {patientCount} patients with ~{totalResourcesPerPatient} resources each..." +
                         (generationSeed.HasValue ? $" (seed={generationSeed.Value})" : string.Empty));

        // ------------------------------------------------------------------
        // Shared infrastructure — uploaded once in the first chunk
        // ------------------------------------------------------------------
        var sharedEntries = new List<Bundle.EntryComponent>
        {
            Entry($"Organization/{ids.Organization}",       OrganizationFactory.Generate(ids.Organization)),
            Entry($"Location/{ids.HospitalLocation}",      LocationFactory.Generate(ids.HospitalLocation, "HOSP", "Main Hospital",        ids.Organization)),
            Entry($"Location/{ids.IcuLocation}",           LocationFactory.Generate(ids.IcuLocation,      "ICU",  "Intensive Care Unit",   ids.Organization)),
            Entry($"Location/{ids.EdLocation}",            LocationFactory.Generate(ids.EdLocation,        "ER",   "Emergency Department",  ids.Organization)),
            Entry($"Location/{ids.StepDownLocation}",      LocationFactory.Generate(ids.StepDownLocation,  "HU",   "Step-Down Unit",        ids.Organization)),
            Entry($"Location/{ids.OutpatientLocation}",    LocationFactory.Create(ids.OutpatientLocation, "OF",   "Outpatient Clinic",     ids.Organization)),
            Entry($"Device/{ids.DevicePulseOx}",      DeviceFactory.Create(ids.DevicePulseOx,    "706689003", "Pulse oximeter",                             null)),
            Entry($"Device/{ids.DeviceVentilator}",   DeviceFactory.Create(ids.DeviceVentilator, "706172005", "Ventilator",                                 null)),
            Entry($"Device/{ids.DeviceCPAP}",         DeviceFactory.Create(ids.DeviceCPAP,       "10776007",  "Continuous positive airway pressure device", null)),
        };

        var sharedPractitionerIds = new List<string>();
        for (var pi = 0; pi < FhirGenerationCodes.Practitioners.Length; pi++)
        {
            var practId = ids.PractitionerId(pi);
            sharedPractitionerIds.Add(practId);
            sharedEntries.Add(Entry($"Practitioner/{practId}", PractitionerFactory.Generate(practId, pi)));
        }

        var sharedMedicationIds = GenerateSharedMedications(sharedEntries, ids);

        // ------------------------------------------------------------------
        // Per-patient generation
        // ------------------------------------------------------------------
        for (var p = 0; p < patientCount; p++)
        {
            var patientSeed = baseSeed + p;
            var patientId = ids.PatientId(p);
            var scenario = FhirGenerationCodes.GetScenarioBySeed(patientSeed);
            var scenarioIdx = FhirGenerationCodes.GetScenarioArrayPosition(scenario);
            var attendingPractId = sharedPractitionerIds[Mod(patientSeed, sharedPractitionerIds.Count)];
            var admittingPractId = sharedPractitionerIds[Mod(patientSeed + 1, sharedPractitionerIds.Count)];
            var gpPractId = sharedPractitionerIds[Mod(patientSeed + 2, sharedPractitionerIds.Count)];
            var encStart = EncounterStart(patientSeed);
            var encEnd = EncounterEnd(patientSeed);
            var encounterId = $"{patientId}-Enc-001";
            var careTeamId = $"{patientId}-CareTeam-001";
            var carePlanId = $"{patientId}-CarePlan-001";
            var patientDeviceId = $"{patientId}-Device-001";
            var primaryDxId = $"{patientId}-Condition-primary";
            patientIds.Add(patientId);

            var entries = new List<Bundle.EntryComponent>();

            // Core anchors — order matters: Patient ? Device ? Encounter ? Diagnoses ? Care
            var patient = PatientFactory.Generate(patientId, patientSeed, gpPractId);
            patient.ManagingOrganization = new ResourceReference($"Organization/{ids.Organization}", "General Test Hospital");
            entries.Add(Entry($"Patient/{patientId}", patient));

            entries.Add(Entry($"Device/{patientDeviceId}",
                DeviceFactory.Generate(patientDeviceId, patientSeed, patientId)));

            // Primary admission diagnosis — from the scenario
            entries.Add(Entry($"Condition/{primaryDxId}",
                ConditionFactory.CreatePrimary(
                    primaryDxId, patientId, encounterId, encStart,
                    scenario.PrimaryDxSnomed, scenario.PrimaryDxDisplay, scenario.PrimaryDxIcd)));

            entries.Add(Entry($"Encounter/{encounterId}",
                EncounterFactory.Generate(
                    encounterId, patientId, encStart, encEnd,
                    attendingPractId, admittingPractId,
                    ids.EdLocation, ids.IcuLocation, ids.StepDownLocation, ids.Organization,
                    primaryDxId, scenario)));

            entries.Add(Entry($"CareTeam/{careTeamId}",
                CareTeamFactory.Generate(careTeamId, patientId, encounterId, attendingPractId, encStart, ids.Organization)));

            entries.Add(Entry($"CarePlan/{carePlanId}",
                CarePlanFactory.Generate(carePlanId, patientId, encounterId, careTeamId, encStart, patientSeed)));

            GenerateScenarioDrivenResources(entries, scenarioIdx, patientId, encounterId,
                encStart, encEnd, primaryDxId, attendingPractId, careTeamId, patientIdPrefix,
                totalResourcesPerPatient, baseSeed, p, sharedPractitionerIds, sharedMedicationIds, config, ids);

            output.WriteLine($"  Patient {patientId}: {entries.Count} entries | scenario={scenario.PrimaryDxDisplay} | " +
                             $"encounter={encounterId} LOS={(encEnd - encStart).TotalDays:F1}d " +
                             $"({encStart:yyyy-MM-dd} ? {encEnd:yyyy-MM-dd})");

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
                    bundles.Add(($"{currentPatientId}_chunk{chunkIndex:D2}", Serialize(currentChunk)));
                    currentChunk = [];
                }
            }
        }

        if (currentChunk.Count > 0)
        {
            chunkIndex++;
            bundles.Add(($"{currentPatientId}_chunk{chunkIndex:D2}", Serialize(currentChunk)));
        }

        output.WriteLine($"Generated {bundles.Count} transaction bundles for {patientCount} patients.");
        return (patientIds, bundles);
    }

    /// <summary>
    /// Generate patients from cohort definitions. This is the primary entry point
    /// for both Automation.UI and BackendE2ETests, ensuring identical generation
    /// logic across both platforms.
    /// </summary>
    public static (List<string> PatientIds, List<(string Name, string Json)> Bundles) GenerateFromCohorts(
        IAutomationOutput output,
        IReadOnlyList<ProfiledMeasureType> measures,
        IReadOnlyList<PatientCohortDefinition> cohorts,
        string patientIdPrefix = "CohortPatient",
        int? generationSeed = null,
        FhirGenerationConfig? config = null)
    {
        var seed = generationSeed.GetValueOrDefault();
        var profiles = PatientCohortDefinition.ExpandProfiles(cohorts, seed);

        if (profiles.Count == 0)
            throw new ArgumentException("Cohorts produced zero patient profiles.", nameof(cohorts));

        // Use the first profile's resource count as the run-level default;
        // per-patient overrides are carried on each profile.
        var defaultResources = profiles[0].ResourcesPerPatient ?? 100;

        return GenerateWithProfiles(output, measures, profiles, defaultResources, patientIdPrefix, generationSeed, config);
    }

    /// <summary>
    /// Generate patients with explicit measure-eligibility profiles that carry
    /// per-measure eligibility. Each profile's <see cref="PatientProfile.MeasureEligibilities"/>
    /// drives encounter type, medication generation, etc.
    /// </summary>
    public static (List<string> PatientIds, List<(string Name, string Json)> Bundles) GenerateWithProfiles(
        IAutomationOutput output,
        IReadOnlyList<ProfiledMeasureType> measures,
        IReadOnlyList<PatientProfile> profiles,
        int totalResourcesPerPatient = DefaultResourcesPerPatient,
        string patientIdPrefix = "ProfilePatient",
        int? generationSeed = null,
        FhirGenerationConfig? config = null)
    {
        if (measures == null || measures.Count == 0)
            throw new ArgumentException("At least one measure is required.", nameof(measures));

        if (profiles == null)
            throw new ArgumentNullException(nameof(profiles));

        if (profiles.Count == 0)
            throw new ArgumentException("At least one patient profile is required.", nameof(profiles));

        if (measures.Count > 1)
        {
            output.WriteLine($"Multi-measure generation: [{string.Join(", ", measures)}]");
        }

        return GenerateWithProfilesCore(
            output,
            measures,
            profiles,
            totalResourcesPerPatient,
            patientIdPrefix,
            generationSeed,
            config: config);
    }

    private static (List<string> PatientIds, List<(string Name, string Json)> Bundles) GenerateWithProfilesCore(
        IAutomationOutput output,
        IReadOnlyList<ProfiledMeasureType> measures,
        IReadOnlyList<PatientProfile> profiles,
        int totalResourcesPerPatient = DefaultResourcesPerPatient,
        string patientIdPrefix = "ProfilePatient",
        int? generationSeed = null,
        FhirGenerationConfig? config = null)
    {
        var patientIds = new List<string>();
        var allEntries = new List<(string PatientId, List<Bundle.EntryComponent> Entries)>();
        var baseSeed = generationSeed.GetValueOrDefault();
        var ids = new SharedIds(patientIdPrefix);

        var qualifyingAllCount = profiles.Count(p => p.QualifiesForAll(measures));
        var nonQualifyingAllCount = profiles.Count(p => p.QualifiesForNone(measures));
        var mixedEligibilityCount = profiles.Count - qualifyingAllCount - nonQualifyingAllCount;
        output.WriteLine($"Generating {profiles.Count} profiled patients ({qualifyingAllCount} qualifying-all, {nonQualifyingAllCount} non-qualifying-all, {mixedEligibilityCount} mixed) " +
                         $"with ~{totalResourcesPerPatient} resources each..." +
                         (generationSeed.HasValue ? $" (seed={generationSeed.Value})" : string.Empty));

        // ------------------------------------------------------------------
        // Shared infrastructure — same as Generate(), plus outpatient location
        // ------------------------------------------------------------------
        var sharedEntries = new List<Bundle.EntryComponent>
        {
            Entry($"Organization/{ids.Organization}",       OrganizationFactory.Generate(ids.Organization)),
            Entry($"Location/{ids.HospitalLocation}",      LocationFactory.Generate(ids.HospitalLocation, "HOSP", "Main Hospital",        ids.Organization)),
            Entry($"Location/{ids.IcuLocation}",           LocationFactory.Generate(ids.IcuLocation,      "ICU",  "Intensive Care Unit",   ids.Organization)),
            Entry($"Location/{ids.EdLocation}",            LocationFactory.Generate(ids.EdLocation,        "ER",   "Emergency Department",  ids.Organization)),
            Entry($"Location/{ids.StepDownLocation}",      LocationFactory.Generate(ids.StepDownLocation,  "HU",   "Step-Down Unit",        ids.Organization)),
            Entry($"Location/{ids.OutpatientLocation}",    LocationFactory.Create(ids.OutpatientLocation, "OF",   "Outpatient Clinic",     ids.Organization)),
            Entry($"Device/{ids.DevicePulseOx}",      DeviceFactory.Create(ids.DevicePulseOx,    "706689003", "Pulse oximeter",                             null)),
            Entry($"Device/{ids.DeviceVentilator}",   DeviceFactory.Create(ids.DeviceVentilator, "706172005", "Ventilator",                                 null)),
            Entry($"Device/{ids.DeviceCPAP}",         DeviceFactory.Create(ids.DeviceCPAP,       "10776007",  "Continuous positive airway pressure device", null)),
        };

        var sharedPractitionerIds = new List<string>();
        for (var pi = 0; pi < FhirGenerationCodes.Practitioners.Length; pi++)
        {
            var practId = ids.PractitionerId(pi);
            sharedPractitionerIds.Add(practId);
            sharedEntries.Add(Entry($"Practitioner/{practId}", PractitionerFactory.Generate(practId, pi)));
        }

        var sharedMedicationIds = GenerateSharedMedications(sharedEntries, ids);

        // ------------------------------------------------------------------
        // Per-patient generation — profile-driven
        // ------------------------------------------------------------------
        for (var p = 0; p < profiles.Count; p++)
        {
            var profile = profiles[p];
            var patientSeed = baseSeed + (profile.SeedOffset ?? p);
            var patientId = ids.PatientId(p);
            var scenario = FhirGenerationCodes.GetScenarioById(profile.ClinicalScenarioId)
                           ?? FhirGenerationCodes.GetScenarioBySeed(patientSeed);
            var scenarioIdx = FhirGenerationCodes.GetScenarioArrayPosition(scenario);
            var attendingPractId = sharedPractitionerIds[Mod(patientSeed, sharedPractitionerIds.Count)];
            var admittingPractId = sharedPractitionerIds[Mod(patientSeed + 1, sharedPractitionerIds.Count)];
            var gpPractId = sharedPractitionerIds[Mod(patientSeed + 2, sharedPractitionerIds.Count)];
            var encounterId = $"{patientId}-Enc-001";
            var careTeamId = $"{patientId}-CareTeam-001";
            var carePlanId = $"{patientId}-CarePlan-001";
            var patientDeviceId = $"{patientId}-Device-001";
            var primaryDxId = $"{patientId}-Condition-primary";
            patientIds.Add(patientId);

            // Encounter dates: inpatient patients use measurement-period dates,
            // non-qualifying use dates well outside the measurement period.
            DateTime encStart, encEnd;
            if (profile.RequiresInpatientEncounter())
            {
                encStart = EncounterStart(patientSeed);
                encEnd = EncounterEnd(patientSeed);
            }
            else
            {
                // Place encounter 2 years before the measurement period so it
                // structurally cannot overlap. Vary by seed for realism.
                encStart = new DateTime(2020, 1 + (Mod(patientSeed, 6)), 1 + (Mod(patientSeed * 3, 28)),
                                        8 + Mod(patientSeed, 4), 0, 0, DateTimeKind.Utc);
                encEnd = encStart.AddHours(2 + Mod(patientSeed, 4));
            }

            var entries = new List<Bundle.EntryComponent>();

            var patient = PatientFactory.Generate(patientId, patientSeed, gpPractId);
            patient.ManagingOrganization = new ResourceReference($"Organization/{ids.Organization}", "General Test Hospital");
            entries.Add(Entry($"Patient/{patientId}", patient));

            entries.Add(Entry($"Device/{patientDeviceId}",
                DeviceFactory.Generate(patientDeviceId, patientSeed, patientId)));

            entries.Add(Entry($"Condition/{primaryDxId}",
                ConditionFactory.CreatePrimary(
                    primaryDxId, patientId, encounterId, encStart,
                    scenario.PrimaryDxSnomed, scenario.PrimaryDxDisplay, scenario.PrimaryDxIcd)));

            if (profile.RequiresInpatientEncounter())
            {
                // Inpatient encounter — qualifies via class, type, and location
                if (profile.RequiresHypoglycemicMedication())
                {
                    entries.Add(Entry($"Encounter/{encounterId}",
                        EncounterFactory.Create(
                            encounterId,
                            patientId,
                            encStart,
                            encEnd,
                            attendingPractId,
                            admittingPractId,
                            ids.EdLocation,
                            ids.IcuLocation,
                            ids.StepDownLocation,
                            ids.Organization,
                            primaryDxId,
                            "32485007",
                            "Hospital admission (procedure)",
                            scenario.PrimaryDxSnomed,
                            scenario.PrimaryDxDisplay,
                            scenario.PrimaryDxIcd,
                            scenario.AdmitSourceCode,
                            scenario.AdmitSourceDisplay,
                            scenario.DischargeDispositionCode,
                            scenario.DischargeDispositionDisplay,
                            scenario.ServiceTypeCode,
                            scenario.ServiceTypeDisplay,
                            "EM",
                            "emergency")));
                }
                else
                {
                    entries.Add(Entry($"Encounter/{encounterId}",
                        EncounterFactory.Generate(
                            encounterId, patientId, encStart, encEnd,
                            attendingPractId, admittingPractId,
                            ids.EdLocation, ids.IcuLocation, ids.StepDownLocation, ids.Organization,
                            primaryDxId, scenario)));
                }
            }
            else
            {
                // Ambulatory encounter — class=AMB, outpatient location, outside measurement period
                entries.Add(Entry($"Encounter/{encounterId}",
                    EncounterFactory.CreateAmbulatory(
                        encounterId, patientId, encStart, encEnd,
                        attendingPractId, ids.OutpatientLocation, ids.Organization,
                        primaryDxId,
                        scenario.PrimaryDxSnomed, scenario.PrimaryDxDisplay, scenario.PrimaryDxIcd)));
            }

            entries.Add(Entry($"CareTeam/{careTeamId}",
                CareTeamFactory.Generate(careTeamId, patientId, encounterId, attendingPractId, encStart, ids.Organization)));

            entries.Add(Entry($"CarePlan/{carePlanId}",
                CarePlanFactory.Generate(carePlanId, patientId, encounterId, careTeamId, encStart, patientSeed)));

            // Add Hypo medication entries when the profile requires it.
            if (profile.RequiresHypoglycemicMedication())
            {
                AddHypoglycemicQualifyingMedicationEntries(entries, patientId, encounterId, attendingPractId, patientSeed, encStart, ids);
            }

            GenerateScenarioDrivenResources(entries, scenarioIdx, patientId, encounterId,
                encStart, encEnd, primaryDxId, attendingPractId, careTeamId, patientIdPrefix,
                totalResourcesPerPatient, baseSeed, p, sharedPractitionerIds, sharedMedicationIds, config, ids);

            var measureEligibilityLabel = string.Join(", ", measures.Select(functionMeasure =>
                {
                    var shortName = functionMeasure switch
                    {
                        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation => "ACH",
                        ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation => "ACH-Daily",
                        ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation => "Hypo",
                        _ => functionMeasure.ToString()
                    };
                    var eligible = profile.QualifiesFor(functionMeasure) ? "Q" : "NQ";
                    return $"{shortName}={eligible}";
                }));

            output.WriteLine($"  Patient {patientId}: {entries.Count} entries [{measureEligibilityLabel}] | scenario={scenario.PrimaryDxDisplay} | " +
                             $"encounter={encounterId} ({encStart:yyyy-MM-dd} ? {encEnd:yyyy-MM-dd})");

            allEntries.Add((patientId, entries));
        }

        // ------------------------------------------------------------------
        // Chunk into batch bundles — same as Generate()
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
                    bundles.Add(($"{currentPatientId}_chunk{chunkIndex:D2}", Serialize(currentChunk)));
                    currentChunk = [];
                }
            }
        }

        if (currentChunk.Count > 0)
        {
            chunkIndex++;
            bundles.Add(($"{currentPatientId}_chunk{chunkIndex:D2}", Serialize(currentChunk)));
        }

        output.WriteLine($"Generated {bundles.Count} batch bundles for {profiles.Count} profiled patients.");
        return (patientIds, bundles);
    }

    private static void AddHypoglycemicQualifyingMedicationEntries(
        List<Bundle.EntryComponent> entries,
        string patientId,
        string encounterId,
        string practitionerId,
        int seed,
        DateTime encounterStart,
        SharedIds ids)
    {
        // Use a Diabetes Medications value-set member from the uploaded Hypoglycemic measure bundle.
        const string insulinRxNorm = "274783";
        const string insulinDisplay = "insulin glargine";
        const string subcutaneousRouteCode = "34206005";
        const string subcutaneousRouteDisplay = "Subcutaneous route";
        const string diabetesIndicationCode = "44054006";
        const string diabetesIndicationDisplay = "Diabetes mellitus type 2";

        // Shared formulary Medication for insulin glargine (RxNorm 274783).
        // The Medication resource itself is already in sharedEntries; we only need
        // the per-patient MedicationRequest and MedicationAdministration.
        var medicationRequestId = $"{patientId}-MedReq-A01";
        var medicationAdministrationId = $"{patientId}-MedAdm-A01";
        var medicationTime = encounterStart.AddHours(1);

        entries.Add(Entry($"MedicationRequest/{medicationRequestId}",
            MedicationRequestFactory.Create(
                medicationRequestId,
                patientId,
                encounterId,
                medicationTime,
                seed,
                practitionerId,
                insulinRxNorm,
                insulinDisplay,
                subcutaneousRouteCode,
                subcutaneousRouteDisplay,
                20,
                "[iU]",
                1,
                false,
                diabetesIndicationCode,
                diabetesIndicationDisplay,
                null,
                ids.HypoInsulinGlargineMedication)));

        entries.Add(Entry($"MedicationAdministration/{medicationAdministrationId}",
            MedicationAdministrationFactory.Create(
                medicationAdministrationId,
                patientId,
                encounterId,
                medicationTime,
                seed,
                practitionerId,
                insulinRxNorm,
                insulinDisplay,
                subcutaneousRouteCode,
                subcutaneousRouteDisplay,
                20,
                "[iU]",
                diabetesIndicationCode,
                diabetesIndicationDisplay,
                false,
                ids.HypoInsulinGlargineMedication)));
    }

    // ------------------------------------------------------------------
    //  Private helpers
    // ------------------------------------------------------------------

    private static Bundle.EntryComponent Entry(string resourceUrl, Resource resource) => new()
    {
        FullUrl = $"http://localhost:8080/fhir/{resourceUrl}",
        Resource = resource,
        Request = new Bundle.RequestComponent { Method = Bundle.HTTPVerb.PUT, Url = resourceUrl }
    };

    private static string Serialize(List<Bundle.EntryComponent> entries)
    {
        var bundle = new Bundle { Type = Bundle.BundleType.Transaction, Entry = entries };
        return JsonSerializer.Serialize(bundle, FhirSerializerOptions.ForFhirWithoutValidation());
    }

    private static DateTime EncounterStart(int index)
    {
        const int baseYear = 2023;
        const int baseMonth = 1;
        var monthOffset = index % 12;
        var dayOffset = (index * 3) % 28;
        var month = ((baseMonth - 1 + monthOffset) % 12) + 1;
        var year = baseYear + (baseMonth - 1 + monthOffset) / 12;
        return new DateTime(year, month, 1 + dayOffset, 6 + (index % 6), 0, 0, DateTimeKind.Utc);
    }

    private static DateTime EncounterEnd(int index) =>
        EncounterStart(index).AddDays(2 + ((index * 7) % 20)).AddHours(4);

    /// <summary>
    /// Generates all bulk resources for a single patient using scenario-driven
    /// resource selection. Resources are picked from clinically appropriate subsets
    /// of the global pools so a pneumonia patient gets antibiotics and chest X-rays,
    /// not insulin and echocardiograms.
    ///
    /// Also builds natural clinical reference chains during generation:
    /// ServiceRequest ? Specimen ? Observation ? DiagnosticReport,
    /// MedicationRequest ? Medication, MedicationAdministration ? MedicationRequest.
    /// </summary>
    private static void GenerateScenarioDrivenResources(
        List<Bundle.EntryComponent> entries,
        int scenarioIdx,
        string patientId,
        string encounterId,
        DateTime encStart,
        DateTime encEnd,
        string primaryDxId,
        string attendingPractId,
        string careTeamId,
        string patientIdPrefix,
        int totalResourcesPerPatient,
        int baseSeed,
        int patientOrdinal,
        List<string> sharedPractitionerIds,
        List<string> sharedMedicationIds,
        FhirGenerationConfig? config,
        SharedIds ids)
    {
        // Build scenario-appropriate index subsets
        var medIndices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalMedicationIndices, ScenarioResourceMap.ScenarioMedicationIndices,
            scenarioIdx, FhirGenerationCodes.Medications.Length);
        var obsIndices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalObservationIndices, ScenarioResourceMap.ScenarioObservationIndices,
            scenarioIdx, FhirGenerationCodes.Observations.Length);
        var procIndices = ScenarioResourceMap.ScenarioProcedureIndices[
            Mod(scenarioIdx, ScenarioResourceMap.ScenarioProcedureIndices.Length)];
        var specIndices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalSpecimenIndices, ScenarioResourceMap.ScenarioSpecimenIndices,
            scenarioIdx, FhirGenerationCodes.Specimens.Length);
        var imgIndices = ScenarioResourceMap.ScenarioImagingIndices[
            Mod(scenarioIdx, ScenarioResourceMap.ScenarioImagingIndices.Length)];
        var srIndices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalServiceRequestIndices, ScenarioResourceMap.ScenarioServiceRequestIndices,
            scenarioIdx, FhirGenerationCodes.ServiceRequests.Length);
        var condIndices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalConditionIndices, ScenarioResourceMap.ScenarioConditionIndices,
            scenarioIdx, FhirGenerationCodes.Conditions.Length);

        var medicationRequestIds = new List<string>();
        var specimenIds = new List<string>();
        var observationIds = new List<string>();
        var conditionIds = new List<string> { primaryDxId };
        var serviceRequestIds = new List<string>();
        var diagnosticReportIds = new List<string>();
        var resourceIndex = 0;
        var distribution = ResolveDistribution(config);
        var includeLowValueOptionalReferences = config?.IncludeLowValueOptionalReferences ?? true;

        foreach (var (resourceType, fraction) in distribution)
        {
            var count = Math.Max(1, (int)(totalResourcesPerPatient * fraction));

            for (var i = 0; i < count; i++)
            {
                resourceIndex++;
                var seed = baseSeed + (patientOrdinal * 31 + i);
                var resourceId = $"{patientId}-{AbbreviateResourceType(resourceType)}-{resourceIndex:D3}";
                var offset = TimeSpan.FromMinutes((double)i / Math.Max(count, 1) * (encEnd - encStart).TotalMinutes);
                var effectiveDate = encStart.Add(offset);
                var practId = sharedPractitionerIds[Mod(seed, sharedPractitionerIds.Count)];

                Resource resource = resourceType switch
                {
                    "Observation" => GenerateScenarioObservation(resourceId, patientId, encounterId, effectiveDate, seed, obsIndices, specimenIds, observationIds, ids),
                    "Condition" => GenerateScenarioCondition(resourceId, patientId, encounterId, effectiveDate, encEnd, seed, condIndices, conditionIds),
                    "Procedure" => GenerateScenarioProcedure(resourceId, patientId, encounterId, effectiveDate, seed, practId, procIndices, conditionIds, ids),
                    "MedicationRequest" => GenerateScenarioMedicationRequest(resourceId, patientId, encounterId, effectiveDate, seed, practId, medIndices, conditionIds, sharedMedicationIds, medicationRequestIds),
                    "MedicationAdministration" => GenerateScenarioMedicationAdministration(resourceId, patientId, encounterId, effectiveDate, seed, medIndices, sharedMedicationIds, medicationRequestIds, practId, includeLowValueOptionalReferences),
                    "DiagnosticReport" => GenerateScenarioDiagnosticReport(resourceId, patientId, encounterId, effectiveDate, seed, observationIds, specimenIds, practId, diagnosticReportIds),
                    "ServiceRequest" => GenerateScenarioServiceRequest(resourceId, patientId, encounterId, effectiveDate, seed, practId, srIndices, conditionIds, serviceRequestIds, ids),
                    "Coverage" => CoverageFactory.Generate(resourceId, patientId, encStart, encEnd, seed),
                    "Specimen" => GenerateScenarioSpecimen(resourceId, patientId, effectiveDate, seed, specIndices, specimenIds, practId),
                    "AllergyIntolerance" => AllergyIntoleranceFactory.Generate(resourceId, patientId, encStart, seed, practId),
                    "Immunization" => ImmunizationFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, ids.HospitalLocation, ids.Organization),
                    "ImagingStudy" => GenerateScenarioImagingStudy(resourceId, patientId, encounterId, effectiveDate, seed, imgIndices, serviceRequestIds, practId, includeLowValueOptionalReferences, ids),
                    "CareTeam" => CareTeamFactory.Generate(resourceId, patientId, encounterId, attendingPractId, effectiveDate, ids.Organization),
                    "CarePlan" => CarePlanFactory.Generate(resourceId, patientId, encounterId, careTeamId, effectiveDate, seed),
                    "DocumentReference" => DocumentReferenceFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, ids.Organization, attendingPractId),
                    "Provenance" => GenerateScenarioProvenance(resourceId, patientId, encounterId, effectiveDate, practId, diagnosticReportIds, includeLowValueOptionalReferences, ids),
                    _ => throw new InvalidOperationException($"Unknown resource type: {resourceType}")
                };

                entries.Add(Entry($"{resourceType}/{resourceId}", resource));
            }
        }

        var listId = $"SyntheticList-{patientId}";
        entries.Add(Entry($"List/{listId}",
            CensusListFactory.Generate(listId, patientId, patientIdPrefix, encStart)));
    }

    // ------------------------------------------------------------------
    //  Scenario-aware resource generators — pick from scenario subsets
    //  and wire up reference chains during creation
    // ------------------------------------------------------------------

    private static Observation GenerateScenarioObservation(
        string id, string patientId, string encounterId, DateTime effective, int seed,
        int[] obsIndices, List<string> specimenIds, List<string> observationIds, SharedIds ids)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(obsIndices, seed, FhirGenerationCodes.Observations.Length);
        var v = FhirGenerationCodes.Observations[poolIdx];
        observationIds.Add(id);
        return ObservationFactory.Create(id, patientId, encounterId, effective,
            v.Code, v.Display, v.Category, v.Unit,
            v.CritLow, v.NormLow, v.NormHigh, v.CritHigh, seed, specimenIds, ids.Organization);
    }

    private static Condition GenerateScenarioCondition(
        string id, string patientId, string encounterId, DateTime onset, DateTime abatement, int seed,
        int[] condIndices, List<string> conditionIds)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(condIndices, seed, FhirGenerationCodes.Conditions.Length);
        var v = FhirGenerationCodes.Conditions[poolIdx];
        conditionIds.Add(id);
        return ConditionFactory.Create(id, patientId, encounterId, onset, abatement, seed,
            v.Code, v.Display, v.IcdCode, v.Category);
    }

    private static Procedure GenerateScenarioProcedure(
        string id, string patientId, string encounterId, DateTime performed, int seed, string practId,
        int[] procIndices, List<string> conditionIds, SharedIds ids)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(procIndices, seed, FhirGenerationCodes.Procedures.Length);
        var v = FhirGenerationCodes.Procedures[poolIdx];
        return ProcedureFactory.Create(id, patientId, encounterId, performed, seed, practId,
            ids.HospitalLocation, ids.Organization,
            v.Code, v.Display, v.BodySiteCode, v.BodySiteDisplay,
            v.OutcomeCode, v.OutcomeDisplay,
            conditionIds.Count > 0 ? conditionIds[seed % conditionIds.Count] : null);
    }

    private static MedicationRequest GenerateScenarioMedicationRequest(
        string id, string patientId, string encounterId, DateTime authored, int seed, string practId,
        int[] medIndices, List<string> conditionIds, List<string> sharedMedicationIds, List<string> medicationRequestIds)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(medIndices, seed, FhirGenerationCodes.Medications.Length);
        var v = FhirGenerationCodes.Medications[poolIdx];
        var reasonConditionId = conditionIds.Count > 0 ? conditionIds[seed % conditionIds.Count] : null;
        // Reference the shared formulary Medication that matches this drug.
        var medicationRefId = poolIdx < sharedMedicationIds.Count ? sharedMedicationIds[poolIdx] : null;
        var req = MedicationRequestFactory.Create(id, patientId, encounterId, authored, seed, practId,
            v.RxCode, v.Display, v.RouteCode, v.RouteDisplay,
            v.DoseValue, v.DoseUnit, v.FreqPerDay, v.Prn,
            v.IndicationSnomed, v.IndicationDisplay, reasonConditionId, medicationRefId);
        medicationRequestIds.Add(id);
        return req;
    }

    private static MedicationAdministration GenerateScenarioMedicationAdministration(
        string id, string patientId, string encounterId, DateTime effective, int seed,
        int[] medIndices, List<string> sharedMedicationIds, List<string> medicationRequestIds, string practId,
        bool includeLowValueOptionalReferences)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(medIndices, seed, FhirGenerationCodes.Medications.Length);
        var v = FhirGenerationCodes.Medications[poolIdx];
        // Reference the shared formulary Medication that matches this drug.
        var medRefId = poolIdx < sharedMedicationIds.Count ? sharedMedicationIds[poolIdx] : null;
        var isIv = v.RouteCode == "47625008";
        var admin = MedicationAdministrationFactory.Create(id, patientId, encounterId, effective, seed, practId,
            v.RxCode, v.Display, v.RouteCode, v.RouteDisplay,
            v.DoseValue, v.DoseUnit, v.IndicationSnomed, v.IndicationDisplay, isIv, medRefId);
        // Optional link: MedicationAdministration.request ? MedicationRequest
        if (includeLowValueOptionalReferences && medicationRequestIds.Count > 0)
            admin.Request = new ResourceReference($"MedicationRequest/{medicationRequestIds[seed % medicationRequestIds.Count]}");
        return admin;
    }

    private static Specimen GenerateScenarioSpecimen(
        string id, string patientId, DateTime collected, int seed,
        int[] specIndices, List<string> specimenIds, string practId)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(specIndices, seed, FhirGenerationCodes.Specimens.Length);
        specimenIds.Add(id);
        var v = FhirGenerationCodes.Specimens[poolIdx];
        return SpecimenFactory.Create(id, patientId, collected, seed,
            v.TypeCode, v.TypeDisplay, v.TypeSystem,
            v.ContainerCode, v.ContainerDisplay,
            v.CollectionMethod, v.BodySiteCode, v.BodySiteDisplay, practId);
    }

    private static DiagnosticReport GenerateScenarioDiagnosticReport(
        string id, string patientId, string encounterId, DateTime effective, int seed,
        List<string> observationIds, List<string> specimenIds, string practId,
        List<string> diagnosticReportIds)
    {
        var report = DiagnosticReportFactory.Generate(id, patientId, encounterId, effective, seed,
            observationIds, specimenIds, practId);
        diagnosticReportIds.Add(id);
        return report;
    }

    private static ServiceRequest GenerateScenarioServiceRequest(
        string id, string patientId, string encounterId, DateTime authored, int seed, string practId,
        int[] srIndices, List<string> conditionIds, List<string> serviceRequestIds, SharedIds ids)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(srIndices, seed, FhirGenerationCodes.ServiceRequests.Length);
        var v = FhirGenerationCodes.ServiceRequests[poolIdx];
        var reasonConditionId = conditionIds.Count > 0 ? conditionIds[seed % conditionIds.Count] : null;
        var sr = ServiceRequestFactory.Create(id, patientId, encounterId, authored, seed, practId,
            v.Code, v.Display, v.IsLab, v.System, reasonConditionId, ids.Organization);
        serviceRequestIds.Add(id);
        return sr;
    }

    private static ImagingStudy GenerateScenarioImagingStudy(
        string id, string patientId, string encounterId, DateTime started, int seed,
        int[] imgIndices, List<string> serviceRequestIds, string practId,
        bool includeLowValueOptionalReferences, SharedIds ids)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(imgIndices, seed, FhirGenerationCodes.ImagingStudies.Length);
        var v = FhirGenerationCodes.ImagingStudies[poolIdx];
        var study = ImagingStudyFactory.Create(id, patientId, encounterId, started, ids.HospitalLocation, practId,
            v.SnomedCode, v.Display, v.Modality,
            v.BodySiteCode, v.BodySiteDisplay, v.ReasonCode, v.ReasonDisplay);
        // Optional link: ImagingStudy.basedOn ? ServiceRequest
        if (includeLowValueOptionalReferences && serviceRequestIds.Count > 0)
        {
            study.BasedOn ??= [];
            study.BasedOn.Add(new ResourceReference($"ServiceRequest/{serviceRequestIds[seed % serviceRequestIds.Count]}"));
        }
        return study;
    }

    private static Provenance GenerateScenarioProvenance(
        string id, string patientId, string encounterId, DateTime recorded, string practId,
        List<string> diagnosticReportIds,
        bool includeLowValueOptionalReferences, SharedIds ids)
    {
        var prov = ProvenanceFactory.Create(id, patientId, encounterId, recorded, practId, ids.Organization);
        // Optional link: Provenance.target to DiagnosticReport
        if (includeLowValueOptionalReferences && diagnosticReportIds.Count > 0)
        {
            prov.Target ??= [];
            prov.Target.Add(new ResourceReference($"DiagnosticReport/{diagnosticReportIds[^1]}"));
        }
        return prov;
    }

    /// <summary>
    /// Generates shared hospital formulary Medication resources — one per
    /// <see cref="FhirGenerationCodes.Medications"/> entry. In a real EHR the
    /// formulary is facility-level; every patient's MedicationRequest /
    /// MedicationAdministration references the same Medication resource.
    /// </summary>
    private static List<string> GenerateSharedMedications(List<Bundle.EntryComponent> sharedEntries, SharedIds ids)
    {
        var medIds = new List<string>(FhirGenerationCodes.Medications.Length + 1);
        for (var i = 0; i < FhirGenerationCodes.Medications.Length; i++)
        {
            var v = FhirGenerationCodes.Medications[i];
            var medId = ids.MedicationId(i);
            medIds.Add(medId);
            sharedEntries.Add(Entry($"Medication/{medId}",
                MedicationFactory.Create(medId, v.RxCode, v.Display, v.DoseValue, v.DoseUnit, v.RouteCode, v.RouteDisplay)));
        }

        // Hypoglycemic-measure-specific insulin glargine (RxNorm 274783) —
        // a separate clinical drug concept from the formulary entry above.
        sharedEntries.Add(Entry($"Medication/{ids.HypoInsulinGlargineMedication}",
            MedicationFactory.Create(ids.HypoInsulinGlargineMedication, "274783", "insulin glargine",
                20, "[iU]", "34206005", "Subcutaneous route")));

        return medIds;
    }

    private static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;
}
