using Hl7.Fhir.Model;
using LantanaGroup.Link.Automation.Generation.ResourceFactories;
using LantanaGroup.Link.Shared.Application.SerDes;
using System.Text.Json;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Automation.Generation;

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
    public const int DefaultPatientCount          = 1;
    public const int DefaultResourcesPerPatient   = 10_200;
    private const int MaxEntriesPerBundle         = 500;

    // Shared infrastructure IDs
    public const string HospitalLocationId  = "Gen-Location-Hospital";
    public const string IcuLocationId       = "Gen-Location-ICU";
    public const string EdLocationId        = "Gen-Location-ED";
    public const string StepDownLocationId  = "Gen-Location-StepDown";
    public const string HospitalOrgId       = "Gen-Org-Hospital";

    private static readonly (string ResourceType, double Fraction)[] ResourceDistribution =
    [
        ("Observation",               0.28),
        ("Condition",                 0.08),
        ("Procedure",                 0.07),
        ("MedicationRequest",         0.06),
        ("MedicationAdministration",  0.07),
        ("DiagnosticReport",          0.06),
        ("ServiceRequest",            0.07),
        ("Coverage",                  0.01),
        ("Specimen",                  0.06),
        ("Medication",                0.03),
        ("AllergyIntolerance",        0.02),
        ("Immunization",              0.03),
        ("ImagingStudy",              0.02),
        ("CareTeam",                  0.01),
        ("CarePlan",                  0.01),
        ("DocumentReference",         0.02),
        ("Provenance",                0.02),
    ];

    public static (List<string> PatientIds, List<(string Name, string Json)> Bundles) Generate(
        ITestOutputHelper output,
        int patientCount = DefaultPatientCount,
        int totalResourcesPerPatient = DefaultResourcesPerPatient,
        string patientIdPrefix = "MegaPatient")
    {
        var patientIds = new List<string>();
        var allEntries = new List<(string PatientId, List<Bundle.EntryComponent> Entries)>();

        output.WriteLine($"Generating {patientCount} patients with ~{totalResourcesPerPatient} resources each...");

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
            var patientId        = $"{patientIdPrefix}-{p + 1:D3}";
            var scenario         = FhirGenerationCodes.ClinicalScenarios[p % FhirGenerationCodes.ClinicalScenarios.Length];
            var attendingPractId = sharedPractitionerIds[p % sharedPractitionerIds.Count];
            var admittingPractId = sharedPractitionerIds[(p + 1) % sharedPractitionerIds.Count];
            var gpPractId        = sharedPractitionerIds[(p + 2) % sharedPractitionerIds.Count];
            var encStart         = EncounterStart(p);
            var encEnd           = EncounterEnd(p);
            var encounterId      = $"{patientId}-Enc-001";
            var careTeamId       = $"{patientId}-CareTeam-001";
            var carePlanId       = $"{patientId}-CarePlan-001";
            var patientDeviceId  = $"{patientId}-Device-001";
            var primaryDxId      = $"{patientId}-Condition-primary";
            patientIds.Add(patientId);

            var entries = new List<Bundle.EntryComponent>();

            // Core anchors — order matters: Patient → Device → Encounter → Diagnoses → Care
            entries.Add(Entry($"Patient/{patientId}",
                PatientFactory.Generate(patientId, p, gpPractId)));

            entries.Add(Entry($"Device/{patientDeviceId}",
                DeviceFactory.Generate(patientDeviceId, p, patientId)));

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
                    primaryDxId, p)));

            entries.Add(Entry($"CareTeam/{careTeamId}",
                CareTeamFactory.Generate(careTeamId, patientId, encounterId, attendingPractId, encStart, HospitalOrgId)));

            entries.Add(Entry($"CarePlan/{carePlanId}",
                CarePlanFactory.Generate(carePlanId, patientId, encounterId, careTeamId, encStart, p)));

            var medicationIds  = new List<string>();
            var specimenIds    = new List<string>();
            var observationIds = new List<string>();
            var conditionIds   = new List<string> { primaryDxId };
            var resourceIndex  = 0;

            foreach (var (resourceType, fraction) in ResourceDistribution)
            {
                var count = Math.Max(1, (int)(totalResourcesPerPatient * fraction));

                for (var i = 0; i < count; i++)
                {
                    resourceIndex++;
                    // Combine patient seed (p) with loop counter (i) so every patient
                    // gets a distinct clinical variation even for the same resource index.
                    var seed          = (p * 31 + i);
                    var resourceId    = $"{patientId}-{resourceType}-{resourceIndex:D5}";
                    var offset        = TimeSpan.FromMinutes((double)i / Math.Max(count, 1) * (encEnd - encStart).TotalMinutes);
                    var effectiveDate = encStart.Add(offset);
                    var practId       = sharedPractitionerIds[(seed) % sharedPractitionerIds.Count];

                    Resource resource = resourceType switch
                    {
                        "Observation"              => ObservationFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, specimenIds, observationIds),
                        "Condition"                => ConditionFactory.Generate(resourceId, patientId, encounterId, effectiveDate, encEnd, seed, conditionIds),
                        "Procedure"                => ProcedureFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, practId, HospitalLocationId, HospitalOrgId, conditionIds),
                        "MedicationRequest"        => MedicationRequestFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, practId, conditionIds),
                        "MedicationAdministration" => MedicationAdministrationFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, medicationIds, practId),
                        "DiagnosticReport"         => DiagnosticReportFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, observationIds, specimenIds, practId),
                        "ServiceRequest"           => ServiceRequestFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, practId, conditionIds),
                        "Coverage"                 => CoverageFactory.Generate(resourceId, patientId, encStart, encEnd, seed),
                        "Specimen"                 => SpecimenFactory.Generate(resourceId, patientId, effectiveDate, seed, specimenIds, practId),
                        "Medication"               => MedicationFactory.Generate(resourceId, seed, medicationIds),
                        "AllergyIntolerance"       => AllergyIntoleranceFactory.Generate(resourceId, patientId, encStart, seed),
                        "Immunization"             => ImmunizationFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, HospitalLocationId),
                        "ImagingStudy"             => ImagingStudyFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, HospitalLocationId),
                        "CareTeam"                 => CareTeamFactory.Generate(resourceId, patientId, encounterId, attendingPractId, effectiveDate, HospitalOrgId),
                        "CarePlan"                 => CarePlanFactory.Generate(resourceId, patientId, encounterId, careTeamId, effectiveDate, seed),
                        "DocumentReference"        => DocumentReferenceFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, HospitalOrgId, attendingPractId),
                        "Provenance"               => ProvenanceFactory.Generate(resourceId, patientId, encounterId, effectiveDate, practId, HospitalOrgId),
                        _ => throw new InvalidOperationException($"Unknown resource type: {resourceType}")
                    };

                    entries.Add(Entry($"{resourceType}/{resourceId}", resource));
                }
            }

            var listId = $"SyntheticList-{patientIdPrefix}-{patientId}";
            entries.Add(Entry($"List/{listId}",
                CensusListFactory.Generate(listId, patientId, patientIdPrefix, encStart)));

            output.WriteLine($"  Patient {patientId}: {entries.Count} entries | scenario={scenario.PrimaryDxDisplay} | " +
                             $"encounter={encounterId} LOS={(encEnd - encStart).TotalDays:F1}d " +
                             $"({encStart:yyyy-MM-dd} → {encEnd:yyyy-MM-dd})");

            allEntries.Add((patientId, entries));
        }

        // ------------------------------------------------------------------
        // Chunk into transaction bundles
        // ------------------------------------------------------------------
        var bundles          = new List<(string Name, string Json)>();
        var currentChunk     = new List<Bundle.EntryComponent>(sharedEntries);
        var currentPatientId = "shared";
        var chunkIndex       = 0;

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

    // ------------------------------------------------------------------
    //  Private helpers
    // ------------------------------------------------------------------

    private static Bundle.EntryComponent Entry(string resourceUrl, Resource resource) => new()
    {
        FullUrl  = $"http://localhost:8080/fhir/{resourceUrl}",
        Resource = resource,
        Request  = new Bundle.RequestComponent { Method = Bundle.HTTPVerb.PUT, Url = resourceUrl }
    };

    private static string Serialize(List<Bundle.EntryComponent> entries)
    {
        var bundle = new Bundle { Type = Bundle.BundleType.Transaction, Entry = entries };
        return JsonSerializer.Serialize(bundle, LinkFhirSerializerOptions.ForFhirWithoutValidation());
    }

    private static DateTime EncounterStart(int index)
    {
        const int baseYear  = 2023;
        const int baseMonth = 1;
        var monthOffset = index % 12;
        var dayOffset   = (index * 3) % 28;
        var month = ((baseMonth - 1 + monthOffset) % 12) + 1;
        var year  = baseYear + (baseMonth - 1 + monthOffset) / 12;
        return new DateTime(year, month, 1 + dayOffset, 6 + (index % 6), 0, 0, DateTimeKind.Utc);
    }

    private static DateTime EncounterEnd(int index) =>
        EncounterStart(index).AddDays(2 + ((index * 7) % 20)).AddHours(4);
}
