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
    public const int DefaultResourcesPerPatient = 10_200;
    private const int MaxEntriesPerBundle = 500;

    // Shared infrastructure IDs
    public const string HospitalLocationId = "Gen-Location-Hospital";
    public const string IcuLocationId = "Gen-Location-ICU";
    public const string EdLocationId = "Gen-Location-ED";
    public const string StepDownLocationId = "Gen-Location-StepDown";
    public const string OutpatientLocationId = "Gen-Location-Outpatient";
    public const string HospitalOrgId = "Gen-Org-Hospital";

    private static readonly (string ResourceType, double Fraction)[] ResourceDistribution =
    [
        ("Observation",               0.28),
        ("Condition",                 0.08),
        ("Procedure",                 0.07),
        ("Medication",                0.03),
        ("MedicationRequest",         0.06),
        ("MedicationAdministration",  0.07),
        ("DiagnosticReport",          0.06),
        ("ServiceRequest",            0.07),
        ("Coverage",                  0.01),
        ("Specimen",                  0.06),
        ("AllergyIntolerance",        0.02),
        ("Immunization",              0.03),
        ("ImagingStudy",              0.02),
        ("CareTeam",                  0.01),
        ("CarePlan",                  0.01),
        ("DocumentReference",         0.02),
        ("Provenance",                0.02),
    ];

    public static (List<string> PatientIds, List<(string Name, string Json)> Bundles) Generate(
        IAutomationOutput output,
        int patientCount = DefaultPatientCount,
        int totalResourcesPerPatient = DefaultResourcesPerPatient,
        string patientIdPrefix = "MegaPatient",
        int? generationSeed = null)
    {
        var patientIds = new List<string>();
        var allEntries = new List<(string PatientId, List<Bundle.EntryComponent> Entries)>();
        var baseSeed = generationSeed.GetValueOrDefault();

        output.WriteLine($"Generating {patientCount} patients with ~{totalResourcesPerPatient} resources each..." +
                         (generationSeed.HasValue ? $" (seed={generationSeed.Value})" : string.Empty));

        // ------------------------------------------------------------------
        // Shared infrastructure — uploaded once in the first chunk
        // ------------------------------------------------------------------
        var sharedEntries = new List<Bundle.EntryComponent>
        {
            Entry($"Organization/{HospitalOrgId}",       OrganizationFactory.Generate(HospitalOrgId)),
            Entry($"Location/{HospitalLocationId}",      LocationFactory.Generate(HospitalLocationId, "HOSP", "Main Hospital",        HospitalOrgId)),
            Entry($"Location/{IcuLocationId}",           LocationFactory.Generate(IcuLocationId,      "ICU",  "Intensive Care Unit",   HospitalOrgId)),
            Entry($"Location/{EdLocationId}",            LocationFactory.Generate(EdLocationId,        "ER",   "Emergency Department",  HospitalOrgId)),
            Entry($"Location/{StepDownLocationId}",      LocationFactory.Generate(StepDownLocationId,  "HU",   "Step-Down Unit",        HospitalOrgId)),
            Entry($"Location/{OutpatientLocationId}",    LocationFactory.Create(OutpatientLocationId, "OF",   "Outpatient Clinic",     HospitalOrgId)),
            Entry("Device/Gen-Device-PulseOx",      DeviceFactory.Create("Gen-Device-PulseOx",    "706689003", "Pulse oximeter",                             null)),
            Entry("Device/Gen-Device-Ventilator",   DeviceFactory.Create("Gen-Device-Ventilator", "706172005", "Ventilator",                                 null)),
            Entry("Device/Gen-Device-CPAP",         DeviceFactory.Create("Gen-Device-CPAP",       "10776007",  "Continuous positive airway pressure device", null)),
        };

        var sharedPractitionerIds = new List<string>();
        for (var pi = 0; pi < FhirGenerationCodes.Practitioners.Length; pi++)
        {
            var practId = $"{patientIdPrefix}-Pract-{pi + 1:D3}";
            sharedPractitionerIds.Add(practId);
            sharedEntries.Add(Entry($"Practitioner/{practId}", PractitionerFactory.Generate(practId, pi)));
        }

        // ------------------------------------------------------------------
        // Per-patient generation
        // ------------------------------------------------------------------
        for (var p = 0; p < patientCount; p++)
        {
            var patientSeed = baseSeed + p;
            var patientId = $"{patientIdPrefix}-{p + 1:D3}";
            var scenario = FhirGenerationCodes.ClinicalScenarios[Mod(patientSeed, FhirGenerationCodes.ClinicalScenarios.Length)];
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
            patient.ManagingOrganization = new ResourceReference($"Organization/{HospitalOrgId}", "General Test Hospital");
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
                    EdLocationId, IcuLocationId, StepDownLocationId, HospitalOrgId,
                    primaryDxId, patientSeed)));

            entries.Add(Entry($"CareTeam/{careTeamId}",
                CareTeamFactory.Generate(careTeamId, patientId, encounterId, attendingPractId, encStart, HospitalOrgId)));

            entries.Add(Entry($"CarePlan/{carePlanId}",
                CarePlanFactory.Generate(carePlanId, patientId, encounterId, careTeamId, encStart, patientSeed)));

            var scenarioIdx = Mod(patientSeed, FhirGenerationCodes.ClinicalScenarios.Length);
            GenerateScenarioDrivenResources(entries, scenarioIdx, patientId, encounterId,
                encStart, encEnd, primaryDxId, attendingPractId, careTeamId, patientIdPrefix,
                totalResourcesPerPatient, baseSeed, p, sharedPractitionerIds);

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
    /// Generate patients with explicit measure-eligibility profiles.
    /// Each <see cref="PatientProfile"/> controls whether the patient qualifies
    /// for the measure's Initial Population. Seeds are deterministic and repeatable.
    /// <para>
    /// This method does NOT alter the existing <see cref="Generate"/> code path.
    /// </para>
    /// </summary>
    public static (List<string> PatientIds, List<(string Name, string Json)> Bundles) GenerateWithProfiles(
        IAutomationOutput output,
        IReadOnlyList<PatientProfile> profiles,
        int totalResourcesPerPatient = DefaultResourcesPerPatient,
        string patientIdPrefix = "ProfilePatient",
        int? generationSeed = null)
    {
        return GenerateWithProfiles(
            output,
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
            profiles,
            totalResourcesPerPatient,
            patientIdPrefix,
            generationSeed);
    }

    /// <summary>
    /// Generate patients with explicit measure-eligibility profiles in the context
    /// of a designated measure.
    /// </summary>
    public static (List<string> PatientIds, List<(string Name, string Json)> Bundles) GenerateWithProfiles(
        IAutomationOutput output,
        ProfiledMeasureType measure,
        IReadOnlyList<PatientProfile> profiles,
        int totalResourcesPerPatient = DefaultResourcesPerPatient,
        string patientIdPrefix = "ProfilePatient",
        int? generationSeed = null)
    {
        return GenerateWithProfiles(output, [measure], profiles, totalResourcesPerPatient, patientIdPrefix, generationSeed);
    }

    /// <summary>
    /// Generate patients with explicit measure-eligibility profiles that must
    /// qualify (or not qualify) for ALL specified measures simultaneously.
    /// Qualifying patients satisfy the criteria of every measure; non-qualifying
    /// patients miss at least one (typically all) measures.
    /// </summary>
    public static (List<string> PatientIds, List<(string Name, string Json)> Bundles) GenerateWithProfiles(
        IAutomationOutput output,
        IReadOnlyList<ProfiledMeasureType> measures,
        IReadOnlyList<PatientProfile> profiles,
        int totalResourcesPerPatient = DefaultResourcesPerPatient,
        string patientIdPrefix = "ProfilePatient",
        int? generationSeed = null)
    {
        if (measures == null || measures.Count == 0)
            throw new ArgumentException("At least one measure is required.", nameof(measures));

        if (profiles == null)
            throw new ArgumentNullException(nameof(profiles));

        if (profiles.Count == 0)
            throw new ArgumentException("At least one patient profile is required.", nameof(profiles));

        // For multi-measure: qualifying patients need to satisfy the most restrictive
        // generation requirements. If any measure requires diabetic medication (Hypo),
        // qualifying patients get it. This ensures they qualify for ALL measures.
        var requireDiabeticMed = measures.Any(m =>
            m == ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation);

        if (measures.Count > 1)
        {
            output.WriteLine($"Multi-measure generation: [{string.Join(", ", measures)}] " +
                             $"(requireDiabeticMedForQualifying={requireDiabeticMed})");
        }

        return GenerateWithProfilesForNhsnAcuteCareHospital(
            output,
            profiles,
            totalResourcesPerPatient,
            patientIdPrefix,
            generationSeed,
            requireDiabeticMedicationForQualifying: requireDiabeticMed);
    }

    private static (List<string> PatientIds, List<(string Name, string Json)> Bundles) GenerateWithProfilesForNhsnAcuteCareHospital(
        IAutomationOutput output,
        IReadOnlyList<PatientProfile> profiles,
        int totalResourcesPerPatient = DefaultResourcesPerPatient,
        string patientIdPrefix = "ProfilePatient",
        int? generationSeed = null,
        bool requireDiabeticMedicationForQualifying = false)
    {
        var patientIds = new List<string>();
        var allEntries = new List<(string PatientId, List<Bundle.EntryComponent> Entries)>();
        var baseSeed = generationSeed.GetValueOrDefault();

        var qualifyingCount = profiles.Count(p => p.Eligibility == MeasureEligibility.Qualifying);
        var nonQualifyingCount = profiles.Count - qualifyingCount;
        output.WriteLine($"Generating {profiles.Count} profiled patients ({qualifyingCount} qualifying, {nonQualifyingCount} non-qualifying) " +
                         $"with ~{totalResourcesPerPatient} resources each..." +
                         (generationSeed.HasValue ? $" (seed={generationSeed.Value})" : string.Empty));

        // ------------------------------------------------------------------
        // Shared infrastructure — same as Generate(), plus outpatient location
        // ------------------------------------------------------------------
        var sharedEntries = new List<Bundle.EntryComponent>
        {
            Entry($"Organization/{HospitalOrgId}",       OrganizationFactory.Generate(HospitalOrgId)),
            Entry($"Location/{HospitalLocationId}",      LocationFactory.Generate(HospitalLocationId, "HOSP", "Main Hospital",        HospitalOrgId)),
            Entry($"Location/{IcuLocationId}",           LocationFactory.Generate(IcuLocationId,      "ICU",  "Intensive Care Unit",   HospitalOrgId)),
            Entry($"Location/{EdLocationId}",            LocationFactory.Generate(EdLocationId,        "ER",   "Emergency Department",  HospitalOrgId)),
            Entry($"Location/{StepDownLocationId}",      LocationFactory.Generate(StepDownLocationId,  "HU",   "Step-Down Unit",        HospitalOrgId)),
            Entry($"Location/{OutpatientLocationId}",    LocationFactory.Create(OutpatientLocationId, "OF",   "Outpatient Clinic",     HospitalOrgId)),
            Entry("Device/Gen-Device-PulseOx",      DeviceFactory.Create("Gen-Device-PulseOx",    "706689003", "Pulse oximeter",                             null)),
            Entry("Device/Gen-Device-Ventilator",   DeviceFactory.Create("Gen-Device-Ventilator", "706172005", "Ventilator",                                 null)),
            Entry("Device/Gen-Device-CPAP",         DeviceFactory.Create("Gen-Device-CPAP",       "10776007",  "Continuous positive airway pressure device", null)),
        };

        var sharedPractitionerIds = new List<string>();
        for (var pi = 0; pi < FhirGenerationCodes.Practitioners.Length; pi++)
        {
            var practId = $"{patientIdPrefix}-Pract-{pi + 1:D3}";
            sharedPractitionerIds.Add(practId);
            sharedEntries.Add(Entry($"Practitioner/{practId}", PractitionerFactory.Generate(practId, pi)));
        }

        // ------------------------------------------------------------------
        // Per-patient generation — profile-driven
        // ------------------------------------------------------------------
        for (var p = 0; p < profiles.Count; p++)
        {
            var profile = profiles[p];
            var patientSeed = baseSeed + (profile.SeedOffset ?? p);
            var patientId = $"{patientIdPrefix}-{p + 1:D3}";
            var scenario = FhirGenerationCodes.ClinicalScenarios[Mod(patientSeed, FhirGenerationCodes.ClinicalScenarios.Length)];
            var attendingPractId = sharedPractitionerIds[Mod(patientSeed, sharedPractitionerIds.Count)];
            var admittingPractId = sharedPractitionerIds[Mod(patientSeed + 1, sharedPractitionerIds.Count)];
            var gpPractId = sharedPractitionerIds[Mod(patientSeed + 2, sharedPractitionerIds.Count)];
            var encounterId = $"{patientId}-Enc-001";
            var careTeamId = $"{patientId}-CareTeam-001";
            var carePlanId = $"{patientId}-CarePlan-001";
            var patientDeviceId = $"{patientId}-Device-001";
            var primaryDxId = $"{patientId}-Condition-primary";
            patientIds.Add(patientId);

            // Encounter dates: qualifying patients use measurement-period dates,
            // non-qualifying use dates well outside the measurement period.
            DateTime encStart, encEnd;
            if (profile.Eligibility == MeasureEligibility.Qualifying)
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
            patient.ManagingOrganization = new ResourceReference($"Organization/{HospitalOrgId}", "General Test Hospital");
            entries.Add(Entry($"Patient/{patientId}", patient));

            entries.Add(Entry($"Device/{patientDeviceId}",
                DeviceFactory.Generate(patientDeviceId, patientSeed, patientId)));

            entries.Add(Entry($"Condition/{primaryDxId}",
                ConditionFactory.CreatePrimary(
                    primaryDxId, patientId, encounterId, encStart,
                    scenario.PrimaryDxSnomed, scenario.PrimaryDxDisplay, scenario.PrimaryDxIcd)));

            if (profile.Eligibility == MeasureEligibility.Qualifying)
            {
                // Inpatient encounter — qualifies via class, type, and location
                if (requireDiabeticMedicationForQualifying)
                {
                    entries.Add(Entry($"Encounter/{encounterId}",
                        EncounterFactory.Create(
                            encounterId,
                            patientId,
                            encStart,
                            encEnd,
                            attendingPractId,
                            admittingPractId,
                            EdLocationId,
                            IcuLocationId,
                            StepDownLocationId,
                            HospitalOrgId,
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
                            EdLocationId, IcuLocationId, StepDownLocationId, HospitalOrgId,
                            primaryDxId, patientSeed)));
                }
            }
            else
            {
                // Ambulatory encounter — class=AMB, outpatient location, outside measurement period
                entries.Add(Entry($"Encounter/{encounterId}",
                    EncounterFactory.CreateAmbulatory(
                        encounterId, patientId, encStart, encEnd,
                        attendingPractId, OutpatientLocationId, HospitalOrgId,
                        primaryDxId,
                        scenario.PrimaryDxSnomed, scenario.PrimaryDxDisplay, scenario.PrimaryDxIcd)));
            }

            entries.Add(Entry($"CareTeam/{careTeamId}",
                CareTeamFactory.Generate(careTeamId, patientId, encounterId, attendingPractId, encStart, HospitalOrgId)));

            entries.Add(Entry($"CarePlan/{carePlanId}",
                CarePlanFactory.Generate(carePlanId, patientId, encounterId, careTeamId, encStart, patientSeed)));

            if (requireDiabeticMedicationForQualifying && profile.Eligibility == MeasureEligibility.Qualifying)
            {
                AddHypoglycemicQualifyingMedicationEntries(entries, patientId, encounterId, attendingPractId, patientSeed, encStart);
            }

            var scenarioIdx = Mod(patientSeed, FhirGenerationCodes.ClinicalScenarios.Length);
            GenerateScenarioDrivenResources(entries, scenarioIdx, patientId, encounterId,
                encStart, encEnd, primaryDxId, attendingPractId, careTeamId, patientIdPrefix,
                totalResourcesPerPatient, baseSeed, p, sharedPractitionerIds);

            var tag = profile.Eligibility == MeasureEligibility.Qualifying ? "QUALIFYING" : "NON-QUALIFYING";
            output.WriteLine($"  Patient {patientId}: {entries.Count} entries [{tag}] | scenario={scenario.PrimaryDxDisplay} | " +
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
        DateTime encounterStart)
    {
        // Use a Diabetes Medications value-set member from the uploaded Hypoglycemic measure bundle.
        const string insulinRxNorm = "274783";
        const string insulinDisplay = "insulin glargine";
        const string subcutaneousRouteCode = "34206005";
        const string subcutaneousRouteDisplay = "Subcutaneous route";
        const string diabetesIndicationCode = "44054006";
        const string diabetesIndicationDisplay = "Diabetes mellitus type 2";

        var medicationId = $"{patientId}-Medication-ADD-001";
        var medicationRequestId = $"{patientId}-MedicationRequest-ADD-001";
        var medicationAdministrationId = $"{patientId}-MedicationAdministration-ADD-001";
        var medicationTime = encounterStart.AddHours(1);

        entries.Add(Entry($"Medication/{medicationId}",
            MedicationFactory.Create(
                medicationId,
                insulinRxNorm,
                insulinDisplay,
                20,
                "[iU]",
                subcutaneousRouteCode,
                subcutaneousRouteDisplay)));

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
                diabetesIndicationDisplay)));

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
                infusionPeriod: false,
                medicationRefId: null)));
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
    /// ServiceRequest → Specimen → Observation → DiagnosticReport,
    /// MedicationRequest → Medication, MedicationAdministration → MedicationRequest.
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
        List<string> sharedPractitionerIds)
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

        var medicationIds = new List<string>();
        var medicationRequestIds = new List<string>();
        var specimenIds = new List<string>();
        var observationIds = new List<string>();
        var conditionIds = new List<string> { primaryDxId };
        var serviceRequestIds = new List<string>();
        var diagnosticReportIds = new List<string>();
        var resourceIndex = 0;

        foreach (var (resourceType, fraction) in ResourceDistribution)
        {
            var count = Math.Max(1, (int)(totalResourcesPerPatient * fraction));

            for (var i = 0; i < count; i++)
            {
                resourceIndex++;
                var seed = baseSeed + (patientOrdinal * 31 + i);
                var resourceId = $"{patientId}-{resourceType}-{resourceIndex:D5}";
                var offset = TimeSpan.FromMinutes((double)i / Math.Max(count, 1) * (encEnd - encStart).TotalMinutes);
                var effectiveDate = encStart.Add(offset);
                var practId = sharedPractitionerIds[Mod(seed, sharedPractitionerIds.Count)];

                Resource resource = resourceType switch
                {
                    "Observation" => GenerateScenarioObservation(resourceId, patientId, encounterId, effectiveDate, seed, obsIndices, specimenIds, observationIds),
                    "Condition" => GenerateScenarioCondition(resourceId, patientId, encounterId, effectiveDate, encEnd, seed, condIndices, conditionIds),
                    "Procedure" => GenerateScenarioProcedure(resourceId, patientId, encounterId, effectiveDate, seed, practId, procIndices, conditionIds),
                    "Medication" => MedicationFactory.Generate(resourceId, ScenarioResourceMap.PickIndex(medIndices, seed, FhirGenerationCodes.Medications.Length), medicationIds),
                    "MedicationRequest" => GenerateScenarioMedicationRequest(resourceId, patientId, encounterId, effectiveDate, seed, practId, medIndices, conditionIds, medicationIds, medicationRequestIds),
                    "MedicationAdministration" => GenerateScenarioMedicationAdministration(resourceId, patientId, encounterId, effectiveDate, seed, medIndices, medicationIds, medicationRequestIds, practId),
                    "DiagnosticReport" => GenerateScenarioDiagnosticReport(resourceId, patientId, encounterId, effectiveDate, seed, observationIds, specimenIds, practId, diagnosticReportIds),
                    "ServiceRequest" => GenerateScenarioServiceRequest(resourceId, patientId, encounterId, effectiveDate, seed, practId, srIndices, conditionIds, serviceRequestIds),
                    "Coverage" => CoverageFactory.Generate(resourceId, patientId, encStart, encEnd, seed),
                    "Specimen" => GenerateScenarioSpecimen(resourceId, patientId, effectiveDate, seed, specIndices, specimenIds, practId),
                    "AllergyIntolerance" => AllergyIntoleranceFactory.Generate(resourceId, patientId, encStart, seed, practId),
                    "Immunization" => ImmunizationFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, HospitalLocationId),
                    "ImagingStudy" => GenerateScenarioImagingStudy(resourceId, patientId, encounterId, effectiveDate, seed, imgIndices, serviceRequestIds, practId),
                    "CareTeam" => CareTeamFactory.Generate(resourceId, patientId, encounterId, attendingPractId, effectiveDate, HospitalOrgId),
                    "CarePlan" => CarePlanFactory.Generate(resourceId, patientId, encounterId, careTeamId, effectiveDate, seed),
                    "DocumentReference" => DocumentReferenceFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, HospitalOrgId, attendingPractId),
                    "Provenance" => GenerateScenarioProvenance(resourceId, patientId, encounterId, effectiveDate, practId, diagnosticReportIds),
                    _ => throw new InvalidOperationException($"Unknown resource type: {resourceType}")
                };

                entries.Add(Entry($"{resourceType}/{resourceId}", resource));
            }
        }

        var listId = $"SyntheticList-{patientIdPrefix}-{patientId}";
        entries.Add(Entry($"List/{listId}",
            CensusListFactory.Generate(listId, patientId, patientIdPrefix, encStart)));
    }

    // ------------------------------------------------------------------
    //  Scenario-aware resource generators — pick from scenario subsets
    //  and wire up reference chains during creation
    // ------------------------------------------------------------------

    private static Observation GenerateScenarioObservation(
        string id, string patientId, string encounterId, DateTime effective, int seed,
        int[] obsIndices, List<string> specimenIds, List<string> observationIds)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(obsIndices, seed, FhirGenerationCodes.Observations.Length);
        var v = FhirGenerationCodes.Observations[poolIdx];
        observationIds.Add(id);
        return ObservationFactory.Create(id, patientId, encounterId, effective,
            v.Code, v.Display, v.Category, v.Unit,
            v.CritLow, v.NormLow, v.NormHigh, v.CritHigh, seed, specimenIds);
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
        int[] procIndices, List<string> conditionIds)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(procIndices, seed, FhirGenerationCodes.Procedures.Length);
        var v = FhirGenerationCodes.Procedures[poolIdx];
        return ProcedureFactory.Create(id, patientId, encounterId, performed, seed, practId,
            HospitalLocationId, HospitalOrgId,
            v.Code, v.Display, v.BodySiteCode, v.BodySiteDisplay,
            v.OutcomeCode, v.OutcomeDisplay,
            conditionIds.Count > 0 ? conditionIds[seed % conditionIds.Count] : null);
    }

    private static MedicationRequest GenerateScenarioMedicationRequest(
        string id, string patientId, string encounterId, DateTime authored, int seed, string practId,
        int[] medIndices, List<string> conditionIds, List<string> medicationIds, List<string> medicationRequestIds)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(medIndices, seed, FhirGenerationCodes.Medications.Length);
        var v = FhirGenerationCodes.Medications[poolIdx];
        var reasonConditionId = conditionIds.Count > 0 ? conditionIds[seed % conditionIds.Count] : null;
        var medicationRefId = medicationIds.Count > 0 ? medicationIds[seed % medicationIds.Count] : null;
        var req = MedicationRequestFactory.Create(id, patientId, encounterId, authored, seed, practId,
            v.RxCode, v.Display, v.RouteCode, v.RouteDisplay,
            v.DoseValue, v.DoseUnit, v.FreqPerDay, v.Prn,
            v.IndicationSnomed, v.IndicationDisplay, reasonConditionId, medicationRefId);
        medicationRequestIds.Add(id);
        return req;
    }

    private static MedicationAdministration GenerateScenarioMedicationAdministration(
        string id, string patientId, string encounterId, DateTime effective, int seed,
        int[] medIndices, List<string> medicationIds, List<string> medicationRequestIds, string practId)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(medIndices, seed, FhirGenerationCodes.Medications.Length);
        var v = FhirGenerationCodes.Medications[poolIdx];
        var medRefId = medicationIds.Count > 0 ? medicationIds[seed % medicationIds.Count] : null;
        var isIv = v.RouteCode == "47625008";
        var admin = MedicationAdministrationFactory.Create(id, patientId, encounterId, effective, seed, practId,
            v.RxCode, v.Display, v.RouteCode, v.RouteDisplay,
            v.DoseValue, v.DoseUnit, v.IndicationSnomed, v.IndicationDisplay, isIv, medRefId);
        // Wire MedicationAdministration.request → MedicationRequest
        if (medicationRequestIds.Count > 0)
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
        int[] srIndices, List<string> conditionIds, List<string> serviceRequestIds)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(srIndices, seed, FhirGenerationCodes.ServiceRequests.Length);
        var v = FhirGenerationCodes.ServiceRequests[poolIdx];
        var reasonConditionId = conditionIds.Count > 0 ? conditionIds[seed % conditionIds.Count] : null;
        var sr = ServiceRequestFactory.Create(id, patientId, encounterId, authored, seed, practId,
            v.Code, v.Display, v.IsLab, v.System, reasonConditionId);
        serviceRequestIds.Add(id);
        return sr;
    }

    private static ImagingStudy GenerateScenarioImagingStudy(
        string id, string patientId, string encounterId, DateTime started, int seed,
        int[] imgIndices, List<string> serviceRequestIds, string practId)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(imgIndices, seed, FhirGenerationCodes.ImagingStudies.Length);
        var v = FhirGenerationCodes.ImagingStudies[poolIdx];
        var study = ImagingStudyFactory.Create(id, patientId, encounterId, started, HospitalLocationId, practId,
            v.SnomedCode, v.Display, v.Modality,
            v.BodySiteCode, v.BodySiteDisplay, v.ReasonCode, v.ReasonDisplay);
        // Wire ImagingStudy.basedOn → ServiceRequest
        if (serviceRequestIds.Count > 0)
        {
            study.BasedOn ??= [];
            study.BasedOn.Add(new ResourceReference($"ServiceRequest/{serviceRequestIds[seed % serviceRequestIds.Count]}"));
        }
        return study;
    }

    private static Provenance GenerateScenarioProvenance(
        string id, string patientId, string encounterId, DateTime recorded, string practId,
        List<string> diagnosticReportIds)
    {
        var prov = ProvenanceFactory.Create(id, patientId, encounterId, recorded, practId, HospitalOrgId);
        // Wire Provenance.target to include a DiagnosticReport when available
        if (diagnosticReportIds.Count > 0)
        {
            prov.Target ??= [];
            prov.Target.Add(new ResourceReference($"DiagnosticReport/{diagnosticReportIds[^1]}"));
        }
        return prov;
    }

    private static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;
}
