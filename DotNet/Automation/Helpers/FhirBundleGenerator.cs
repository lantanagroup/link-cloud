using System.Text.Json;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Automation.Helpers;

/// <summary>
/// Generates synthetic FHIR R4 transaction bundles for E2E / stress / volume tests.
///
/// Clinical design goals:
///   - Every patient has realistic US-Core-aligned demographics (multiple identifiers,
///     race/ethnicity extensions, varied gender, telecom, emergency contact).
///   - Each patient gets a primary inpatient Encounter with participants, hospitalization,
///     and location history, plus an optional follow-up outpatient Encounter.
///   - All clinical resources reference the correct Patient, Encounter, Practitioner,
///     and Specimen where applicable.
///   - Observations carry proper UCUM units, reference ranges, and use effectivePeriod
///     (matching real Epic exports) with back-references to Specimens.
///   - The full ACH NHSN data-requirement surface is covered: Patient, Encounter,
///     Location, Device, Condition, Observation, DiagnosticReport, MedicationRequest,
///     MedicationAdministration, Medication, Procedure, ServiceRequest, Coverage,
///     Specimen, AllergyIntolerance, Immunization, ImagingStudy, CareTeam, CarePlan,
///     DocumentReference, Provenance, Practitioner, Organization.
///   - FHIR List resources are generated per-patient so the census pipeline can
///     discover them without static embedded fixtures.
///
/// Bundles are chunked to stay within FHIR server transaction size limits (500 entries).
/// </summary>
public static class FhirBundleGenerator
{
    public const int DefaultPatientCount = 1;
    public const int DefaultResourcesPerPatient = 10_200;
    private const int MaxEntriesPerBundle = 500;

    // ---------------------------------------------------------------
    //  Shared infrastructure ID constants
    // ---------------------------------------------------------------

    private const string HospitalLocationId = "Gen-Location-Hospital";
    private const string IcuLocationId = "Gen-Location-ICU";
    private const string EdLocationId = "Gen-Location-ED";
    private const string StepDownLocationId = "Gen-Location-StepDown";
    private const string PulseOxDeviceId = "Gen-Device-PulseOx";
    private const string VentilatorDeviceId = "Gen-Device-Ventilator";
    private const string CpapDeviceId = "Gen-Device-CPAP";
    private const string HospitalOrgId = "Gen-Org-Hospital";

    // ---------------------------------------------------------------
    //  Resource-type distribution (must sum to ≤ 1.0; remainder goes to Observation)
    // ---------------------------------------------------------------

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

    // ---------------------------------------------------------------
    //  Clinical code tables
    // ---------------------------------------------------------------

    private static readonly (string Code, string Display, string Category, string Unit, double Low, double High)[] ObservationVariants =
    [
        // Vital signs
        ("8867-4",  "Heart rate",                         "vital-signs",  "/min",   50,   120),
        ("8310-5",  "Body temperature",                   "vital-signs",  "Cel",    36.0, 39.5),
        ("59408-5", "Oxygen saturation by Pulse oximetry","vital-signs",  "%",      88,   100),
        ("8302-2",  "Body height",                        "vital-signs",  "cm",     150,  200),
        ("29463-7", "Body weight",                        "vital-signs",  "kg",     50,   130),
        ("55284-4", "Blood pressure systolic and diastolic","vital-signs","mm[Hg]", 90,   180),
        ("9279-1",  "Respiratory rate",                   "vital-signs",  "/min",   12,   30),
        // Laboratory
        ("2093-3",  "Cholesterol [Mass/volume] in Serum", "laboratory",   "mg/dL",  100,  300),
        ("2571-8",  "Triglycerides [Mass/volume] in Serum","laboratory",  "mg/dL",  50,   500),
        ("718-7",   "Hemoglobin [Mass/volume] in Blood",  "laboratory",   "g/dL",   7,    17),
        ("4544-3",  "Hematocrit [Volume Fraction] of Blood","laboratory", "%",      30,   54),
        ("2160-0",  "Creatinine [Mass/volume] in Serum",  "laboratory",   "mg/dL",  0.5,  10),
        ("2345-7",  "Glucose [Mass/volume] in Serum",     "laboratory",   "mg/dL",  60,   400),
        ("6690-2",  "Leukocytes [#/volume] in Blood",     "laboratory",   "10*3/uL",2,    20),
        ("777-3",   "Platelets [#/volume] in Blood",      "laboratory",   "10*3/uL",50,   500),
        ("1742-6",  "Alanine aminotransferase [Enzymatic activity/volume]","laboratory","U/L",7,56),
        ("2340-8",  "Glucose [Mass/volume] in Blood by Automated test strip","laboratory","mg/dL",60,400),
        ("2342-4",  "Glucose [Mass/volume] in Cerebral spinal fluid","laboratory","mg/dL",40,70),
        ("48643-1", "Glomerular filtration rate/1.73 sq M.predicted","laboratory","mL/min/{1.73_m2}",15,120),
        ("6768-6",  "Alkaline phosphatase [Enzymatic activity/volume]","laboratory","U/L",44,147),
        ("2532-0",  "Lactate dehydrogenase [Enzymatic activity/volume]","laboratory","U/L",122,222),
        ("14957-5", "Microalbumin [Mass/volume] in Urine","laboratory","mg/L",0,30),
        ("2089-1",  "Cholesterol in LDL [Mass/volume] in Serum","laboratory","mg/dL",0,300),
        ("14646-4", "Cholesterol in HDL [Mass/volume] in Serum","laboratory","mg/dL",20,100),
        // Microbiology/other
        ("600-7",   "Bacteria identified in Blood by Culture","laboratory","",0,0),
    ];

    private static readonly (string Code, string Display, string IcdCode)[] ConditionVariants =
    [
        ("386661006", "Fever (finding)",                       "R50.9"),
        ("233604007", "Pneumonia (disorder)",                   "J18.9"),
        ("84114007",  "Heart failure (disorder)",               "I50.9"),
        ("49436004",  "Atrial fibrillation (disorder)",         "I48.91"),
        ("44054006",  "Diabetes mellitus type 2 (disorder)",    "E11.9"),
        ("38341003",  "Hypertensive disorder, systemic arterial (disorder)","I10"),
        ("700097003", "Fracture of bone of hip region (disorder)","S72.001A"),
        ("195662009", "Acute renal failure syndrome (disorder)","N17.9"),
        ("13645005",  "Chronic obstructive lung disease (disorder)","J44.1"),
        ("59621000",  "Hypertension (disorder)",               "I10"),
        ("73211009",  "Diabetes mellitus (disorder)",          "E11.9"),
        ("230690007", "Cerebrovascular accident (disorder)",   "I63.9"),
        ("414545008", "Ischemic heart disease (disorder)",     "I25.10"),
        ("40055000",  "Chronic sinusitis (disorder)",          "J32.9"),
        ("267102003", "Sore throat symptom (finding)",         "J02.9"),
    ];

    private static readonly (string Code, string Display)[] ProcedureVariants =
    [
        ("232717009", "Coronary artery bypass grafting (procedure)"),
        ("18286008",  "Catheterization of urinary bladder (procedure)"),
        ("225358003", "Wound care management (procedure)"),
        ("40617009",  "Artificial respiration (procedure)"),
        ("431231003", "Dialysis procedure (procedure)"),
        ("447996002", "Insertion of peripherally inserted central venous catheter (procedure)"),
        ("17404008",  "Tonsillectomy (procedure)"),
        ("34068001",  "Heart valve replacement (procedure)"),
        ("71388002",  "Procedure (procedure)"),
        ("112798008", "Insertion of catheter into urinary bladder (procedure)"),
        ("265764009", "Renal dialysis (procedure)"),
        ("173171007", "Thoracentesis (procedure)"),
        ("312581009", "Bone marrow biopsy (procedure)"),
    ];

    private static readonly (string RxCode, string Display, string RouteCode, string RouteDisplay, double DoseValue, string DoseUnit)[] MedicationVariants =
    [
        ("1049502", "Acetaminophen 325 MG Oral Tablet",          "26643006", "Oral route",          650,  "mg"),
        ("197696",  "Ceftriaxone 250 MG Injection",              "47625008",  "Intravenous route",  2000, "mg"),
        ("309362",  "Enoxaparin 40 MG/0.4 ML Injectable Solution","34206005", "Subcutaneous route", 40,   "mg"),
        ("835829",  "Vancomycin 500 MG Injection",               "47625008",  "Intravenous route",  1000, "mg"),
        ("313002",  "Metoprolol succinate 50 MG Oral Tablet",    "26643006",  "Oral route",         50,   "mg"),
        ("197361",  "Furosemide 40 MG Oral Tablet",              "26643006",  "Oral route",         40,   "mg"),
        ("308460",  "Lisinopril 10 MG Oral Tablet",              "26643006",  "Oral route",         10,   "mg"),
        ("312961",  "Amoxicillin 500 MG Oral Capsule",           "26643006",  "Oral route",         500,  "mg"),
        ("1116635", "Insulin glargine 100 UNT/ML Injectable Solution","34206005","Subcutaneous route",10,"[iU]"),
        ("628971",  "Morphine 2 MG/ML Injectable Solution",      "47625008",  "Intravenous route",  4,    "mg"),
    ];

    private static readonly (string Code, string Display)[] ServiceRequestVariants =
    [
        ("24331-1",  "Lipid panel - Serum or Plasma"),
        ("58410-2",  "CBC panel - Blood by Automated count"),
        ("51990-0",  "Basic metabolic panel - Blood"),
        ("24323-8",  "Comprehensive metabolic 2000 panel - Serum or Plasma"),
        ("409073007","Patient education (procedure)"),
        ("169443000","Diabetic diet"),
        ("408443003","General medical practice"),
        ("182744004","Parenteral nutrition"),
        ("306206005","Referral to service (procedure)"),
        ("11429006",  "Consultation (procedure)"),
    ];

    private static readonly (string Code, string Display, string System)[] SpecimenVariants =
    [
        ("BLDV",      "Blood venous",              "http://terminology.hl7.org/CodeSystem/v2-0488"),
        ("BLDA",      "Blood arterial",            "http://terminology.hl7.org/CodeSystem/v2-0488"),
        ("UR",        "Urine",                     "http://terminology.hl7.org/CodeSystem/v2-0488"),
        ("CSF",       "Cerebral spinal fluid",     "http://terminology.hl7.org/CodeSystem/v2-0488"),
        ("SPT",       "Sputum",                    "http://terminology.hl7.org/CodeSystem/v2-0488"),
        ("SWAB",      "Swab",                      "http://terminology.hl7.org/CodeSystem/v2-0488"),
        ("TISS",      "Tissue",                    "http://terminology.hl7.org/CodeSystem/v2-0488"),
    ];

    private static readonly (string Code, string Display)[] AllergyVariants =
    [
        ("419199007", "Allergy to substance (finding)"),
        ("416098002", "Drug allergy (disorder)"),
        ("414285001", "Food allergy (disorder)"),
        ("232347008", "Allergy to egg protein (finding)"),
        ("91931000",  "Allergy to penicillin (finding)"),
        ("300917003", "Allergy to latex (finding)"),
    ];

    private static readonly (string CvxCode, string Display)[] ImmunizationVariants =
    [
        ("140",  "Influenza, seasonal, injectable, preservative free"),
        ("113",  "Td (adult) preservative free"),
        ("33",   "Pneumococcal polysaccharide PPV23"),
        ("20",   "DTaP"),
        ("115",  "Tdap"),
        ("83",   "Hepatitis A, pediatric/adolescent, 2 dose schedule"),
        ("45",   "Hepatitis B, adult"),
        ("8",    "Hepatitis B, adolescent or pediatric"),
        ("10",   "IPV"),
        ("119",  "Rotavirus, monovalent"),
    ];

    private static readonly (string SnomedCode, string Display, string Modality, string BodySite)[] ImagingVariants =
    [
        ("433236007", "Transthoracic echocardiography (procedure)",  "US", "80891009|Heart structure"),
        ("399208008", "Plain chest X-ray (procedure)",               "DX", "51185008|Thoracic structure"),
        ("77477000",  "Computerized axial tomography (procedure)",   "CT", "69536005|Head structure"),
        ("113091000", "Magnetic resonance imaging (procedure)",      "MR", "69536005|Head structure"),
        ("44179004",  "Fluoroscopy (procedure)",                     "RF", "39607008|Lung structure"),
    ];

    private static readonly (string Gender, string[] GivenNames, string FamilyName, string BirthDate, string Race, string Ethnicity)[] PatientArchetypes =
    [
        ("male",   ["Robert091", "Veronica020"], "Price042",     "1958-04-12", "2106-3|White",               "2186-5|Not Hispanic or Latino"),
        ("female", ["Sandra012", "Lynn003"],     "Nguyen045",    "1972-09-23", "2028-9|Asian",               "2186-5|Not Hispanic or Latino"),
        ("male",   ["James044", "William006"],   "Johnson679",   "1945-11-07", "2054-5|Black or Afr. Am.",   "2186-5|Not Hispanic or Latino"),
        ("female", ["Maria031", "Elena008"],     "Garcia201",    "1988-03-17", "2106-3|White",               "2135-2|Hispanic or Latino"),
        ("other",  ["Casey004"],                 "Thompson021",  "1965-07-30", "2106-3|White",               "2186-5|Not Hispanic or Latino"),
        ("male",   ["David011", "Chen002"],      "Lee034",       "1953-01-14", "2028-9|Asian",               "2186-5|Not Hispanic or Latino"),
        ("female", ["Patricia087"],              "Williams303",  "1979-05-22", "2106-3|White",               "2186-5|Not Hispanic or Latino"),
        ("male",   ["Michael044", "Thomas009"],  "Brown156",     "1991-08-08", "2054-5|Black or Afr. Am.",   "2186-5|Not Hispanic or Latino"),
    ];

    private static readonly (string Family, string Given, string Gender, string Email)[] PractitionerPool =
    [
        ("Green049",     "Erin055",    "female", "Erin055.Green049@testhosp.example.com"),
        ("Schneider097", "Leah096",   "female", "Leah096.Schneider097@testhosp.example.com"),
        ("Becker035",    "Hannah033", "female", "Hannah033.Becker035@testhosp.example.com"),
        ("Reilly981",    "Gabriel934","female", "Gabriel934.Reilly981@testhosp.example.com"),
        ("Martinez091",  "Carlos055", "male",   "Carlos055.Martinez091@testhosp.example.com"),
        ("Patel014",     "Arjun003",  "male",   "Arjun003.Patel014@testhosp.example.com"),
    ];

    // ---------------------------------------------------------------
    //  Encounter offsets — varied LOS so the pipeline sees diversity
    // ---------------------------------------------------------------

    private static DateTime EncounterStart(int patientIndex)
    {
        int baseYear = 2023;
        int baseMonth = 1;
        int monthOffset = patientIndex % 12;
        int dayOffset = (patientIndex * 3) % 28;
        int month = ((baseMonth - 1 + monthOffset) % 12) + 1;
        int year = baseYear + (baseMonth - 1 + monthOffset) / 12;
        return new DateTime(year, month, 1 + dayOffset, 6 + (patientIndex % 6), 0, 0, DateTimeKind.Utc);
    }

    private static DateTime EncounterEnd(int patientIndex)
    {
        // LOS varies: 2 days to 21 days depending on index
        int los = 2 + ((patientIndex * 7) % 20);
        return EncounterStart(patientIndex).AddDays(los).AddHours(4);
    }

    // ---------------------------------------------------------------
    //  Public entry point
    // ---------------------------------------------------------------

    public static (List<string> PatientIds, List<(string Name, string Json)> Bundles) Generate(
        ITestOutputHelper output,
        int patientCount = DefaultPatientCount,
        int totalResourcesPerPatient = DefaultResourcesPerPatient,
        string patientIdPrefix = "MegaPatient")
    {
        var patientIds = new List<string>();
        var allEntries = new List<(string PatientId, List<object> Entries)>();

        output.WriteLine($"Generating {patientCount} patients with ~{totalResourcesPerPatient} resources each...");

        // ------------------------------------------------------------------
        // Shared infrastructure: locations, devices, one hospital org
        // ------------------------------------------------------------------
        var sharedEntries = new List<object>
        {
            MakeEntry($"Organization/{HospitalOrgId}", MakeOrganization(HospitalOrgId, "General Test Hospital", "GTH")),
            MakeEntry($"Location/{HospitalLocationId}", MakeLocation(HospitalLocationId, "HOSP", "Main Hospital", HospitalOrgId)),
            MakeEntry($"Location/{IcuLocationId}",      MakeLocation(IcuLocationId,      "ICU",  "Intensive Care Unit", HospitalOrgId)),
            MakeEntry($"Location/{EdLocationId}",       MakeLocation(EdLocationId,        "ER",   "Emergency Department", HospitalOrgId)),
            MakeEntry($"Location/{StepDownLocationId}", MakeLocation(StepDownLocationId,  "HU",   "Step-Down Unit", HospitalOrgId)),
            MakeEntry($"Device/{PulseOxDeviceId}",      MakeDevice(PulseOxDeviceId, "706689003", "Pulse oximeter", null)),
            MakeEntry($"Device/{VentilatorDeviceId}",   MakeDevice(VentilatorDeviceId, "706172005", "Ventilator", null)),
            MakeEntry($"Device/{CpapDeviceId}",         MakeDevice(CpapDeviceId, "10776007", "Continuous positive airway pressure device", null)),
        };

        // Shared practitioners (referenced by all patients)
        var sharedPractitionerIds = new List<string>();
        for (var pi = 0; pi < PractitionerPool.Length; pi++)
        {
            var practId = $"{patientIdPrefix}-Pract-{pi + 1:D3}";
            sharedPractitionerIds.Add(practId);
            var p = PractitionerPool[pi];
            sharedEntries.Add(MakeEntry($"Practitioner/{practId}", MakePractitioner(practId, p.Family, p.Given, p.Gender, p.Email)));
        }

        // ------------------------------------------------------------------
        // Per-patient generation
        // ------------------------------------------------------------------
        for (var p = 0; p < patientCount; p++)
        {
            var patientId = $"{patientIdPrefix}-{p + 1:D3}";
            patientIds.Add(patientId);

            var archetype = PatientArchetypes[p % PatientArchetypes.Length];
            var attendingPractId = sharedPractitionerIds[p % sharedPractitionerIds.Count];
            var admittingPractId = sharedPractitionerIds[(p + 1) % sharedPractitionerIds.Count];

            var encStart = EncounterStart(p);
            var encEnd = EncounterEnd(p);
            var encounterId = $"{patientId}-Enc-001";
            var careTeamId = $"{patientId}-CareTeam-001";
            var carePlanId = $"{patientId}-CarePlan-001";

            // Per-patient device (CPAP/Ventilator/PulseOx assigned by index)
            var patientDeviceId = $"{patientId}-Device-001";
            var deviceVariants = new[] {
                (PulseOxDeviceId, "706689003", "Pulse oximeter"),
                (VentilatorDeviceId, "706172005", "Ventilator"),
                (CpapDeviceId, "10776007", "CPAP device"),
            };
            var dv = deviceVariants[p % deviceVariants.Length];

            var entries = new List<object>();

            // Patient
            entries.Add(MakeEntry($"Patient/{patientId}",
                MakePatient(patientId, archetype.Gender, archetype.GivenNames, archetype.FamilyName,
                            archetype.BirthDate, archetype.Race, archetype.Ethnicity)));

            // Per-patient device linked to patient
            entries.Add(MakeEntry($"Device/{patientDeviceId}",
                MakeDevice(patientDeviceId, dv.Item2, dv.Item3, patientId)));

            // Primary inpatient encounter
            entries.Add(MakeEntry($"Encounter/{encounterId}",
                MakeEncounter(encounterId, patientId, encStart, encEnd,
                              attendingPractId, admittingPractId,
                              careTeamId, p)));

            // CareTeam + CarePlan (fixed, not in distribution loop)
            entries.Add(MakeEntry($"CareTeam/{careTeamId}",
                MakeCareTeam(careTeamId, patientId, encounterId, attendingPractId, encStart)));
            entries.Add(MakeEntry($"CarePlan/{carePlanId}",
                MakeCarePlan(carePlanId, patientId, encounterId, careTeamId, encStart, p)));

            // ------------------------------------------------------------------
            // Distributed clinical resources
            // ------------------------------------------------------------------
            var medicationIds = new List<string>();
            var specimenIds = new List<string>();
            var observationIds = new List<string>();
            var resourceIndex = 0;

            foreach (var (resourceType, fraction) in ResourceDistribution)
            {
                var count = Math.Max(1, (int)(totalResourcesPerPatient * fraction));

                for (var i = 0; i < count; i++)
                {
                    resourceIndex++;
                    var resourceId = $"{patientId}-{resourceType}-{resourceIndex:D5}";
                    var offset = TimeSpan.FromMinutes((double)i / Math.Max(count, 1) * (encEnd - encStart).TotalMinutes);
                    var effectiveDate = encStart.Add(offset);
                    var practId = sharedPractitionerIds[(i + p) % sharedPractitionerIds.Count];

                    object resource = resourceType switch
                    {
                        "Observation"              => MakeObservation(resourceId, patientId, encounterId, effectiveDate, i, specimenIds, observationIds),
                        "Condition"                => MakeCondition(resourceId, patientId, encounterId, effectiveDate, encEnd, i),
                        "Procedure"                => MakeProcedure(resourceId, patientId, encounterId, effectiveDate, i, practId),
                        "MedicationRequest"        => MakeMedicationRequest(resourceId, patientId, encounterId, effectiveDate, i, practId),
                        "MedicationAdministration" => MakeMedicationAdministration(resourceId, patientId, encounterId, effectiveDate, i, medicationIds, practId),
                        "DiagnosticReport"         => MakeDiagnosticReport(resourceId, patientId, encounterId, effectiveDate, i, observationIds, practId),
                        "ServiceRequest"           => MakeServiceRequest(resourceId, patientId, encounterId, effectiveDate, i, practId),
                        "Coverage"                 => MakeCoverage(resourceId, patientId, encStart, encEnd, i),
                        "Specimen"                 => MakeSpecimen(resourceId, patientId, effectiveDate, i, specimenIds),
                        "Medication"               => MakeMedication(resourceId, i, medicationIds),
                        "AllergyIntolerance"       => MakeAllergyIntolerance(resourceId, patientId, encStart, i),
                        "Immunization"             => MakeImmunization(resourceId, patientId, encounterId, effectiveDate, i),
                        "ImagingStudy"             => MakeImagingStudy(resourceId, patientId, encounterId, effectiveDate, i),
                        "CareTeam"                 => MakeCareTeam(resourceId, patientId, encounterId, attendingPractId, effectiveDate),
                        "CarePlan"                 => MakeCarePlan(resourceId, patientId, encounterId, careTeamId, effectiveDate, i),
                        "DocumentReference"        => MakeDocumentReference(resourceId, patientId, encounterId, effectiveDate, i),
                        "Provenance"               => MakeProvenance(resourceId, patientId, encounterId, effectiveDate, practId),
                        _ => throw new InvalidOperationException($"Unknown resource type: {resourceType}")
                    };

                    entries.Add(MakeEntry($"{resourceType}/{resourceId}", resource));
                }
            }

            // FHIR List — census pipeline hook: one List per patient using well-known naming
            var listId = $"SyntheticList-{patientIdPrefix}-{patientId}";
            entries.Add(MakeEntry($"List/{listId}",
                MakeCensusList(listId, patientId, patientIdPrefix, encStart)));

            output.WriteLine($"  Patient {patientId}: {entries.Count} entries | " +
                             $"encounter={encounterId} LOS={(encEnd - encStart).TotalDays:F1}d " +
                             $"({encStart:yyyy-MM-dd} → {encEnd:yyyy-MM-dd})");

            allEntries.Add((patientId, entries));
        }

        // ------------------------------------------------------------------
        // Chunk into transaction bundles
        // ------------------------------------------------------------------
        var bundles = new List<(string Name, string Json)>();
        var currentChunk = new List<object>(sharedEntries);
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
                    bundles.Add(($"{currentPatientId}_chunk{chunkIndex:D2}", SerializeBundle(currentChunk)));
                    currentChunk = [];
                }
            }
        }

        if (currentChunk.Count > 0)
        {
            chunkIndex++;
            bundles.Add(($"{currentPatientId}_chunk{chunkIndex:D2}", SerializeBundle(currentChunk)));
        }

        output.WriteLine($"Generated {bundles.Count} transaction bundles for {patientCount} patients.");
        return (patientIds, bundles);
    }

    // ---------------------------------------------------------------
    //  Bundle serialization
    // ---------------------------------------------------------------

    private static string SerializeBundle(List<object> entries)
    {
        var bundle = new Dictionary<string, object>
        {
            ["resourceType"] = "Bundle",
            ["type"] = "transaction",
            ["entry"] = entries
        };
        return JsonSerializer.Serialize(bundle);
    }

    // ---------------------------------------------------------------
    //  Resource factories
    // ---------------------------------------------------------------

    private static object MakePatient(string id, string gender, string[] givenNames, string familyName,
        string birthDate, string race, string ethnicity)
    {
        var raceParts = race.Split('|');
        var ethParts = ethnicity.Split('|');
        var mrn = $"MRN-{id.GetHashCode() & 0x7FFFFFFF:D9}";
        var epi = $"E{id.GetHashCode() & 0xFFFFFF:X6}";
        var areaCode = 500 + (Math.Abs(id.GetHashCode()) % 500);
        var lineNum  = Math.Abs(id.GetHashCode() >> 8) % 10000;
        var phone    = $"+1 {areaCode:D3}-555-{lineNum:D4}";

        return new Dictionary<string, object>
        {
            ["resourceType"] = "Patient",
            ["id"] = id,
            ["meta"] = new Dictionary<string, object>
            {
                ["profile"] = new[] { "http://hl7.org/fhir/us/core/StructureDefinition/us-core-patient" }
            },
            ["extension"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["url"] = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-race",
                    ["extension"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["url"] = "ombCategory",
                            ["valueCoding"] = new Dictionary<string, object>
                            {
                                ["system"] = "urn:oid:2.16.840.1.113883.6.238",
                                ["code"] = raceParts[0],
                                ["display"] = raceParts.Length > 1 ? raceParts[1] : raceParts[0]
                            }
                        },
                        new Dictionary<string, object>
                        {
                            ["url"] = "text",
                            ["valueString"] = raceParts.Length > 1 ? raceParts[1] : raceParts[0]
                        }
                    }
                },
                new Dictionary<string, object>
                {
                    ["url"] = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-ethnicity",
                    ["extension"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["url"] = "ombCategory",
                            ["valueCoding"] = new Dictionary<string, object>
                            {
                                ["system"] = "urn:oid:2.16.840.1.113883.6.238",
                                ["code"] = ethParts[0],
                                ["display"] = ethParts.Length > 1 ? ethParts[1] : ethParts[0]
                            }
                        },
                        new Dictionary<string, object>
                        {
                            ["url"] = "text",
                            ["valueString"] = ethParts.Length > 1 ? ethParts[1] : ethParts[0]
                        }
                    }
                }
            },
            ["identifier"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["use"] = "usual",
                    ["system"] = "urn:oid:1.2.840.114350.1.13.93.3.7.3.688884.100",
                    ["type"] = new Dictionary<string, object> { ["text"] = "CEID" },
                    ["value"] = id
                },
                new Dictionary<string, object>
                {
                    ["use"] = "usual",
                    ["system"] = "urn:oid:2.16.840.1.113883.3.16.100.1",
                    ["type"] = new Dictionary<string, object> { ["text"] = "MRN" },
                    ["value"] = mrn
                },
                new Dictionary<string, object>
                {
                    ["use"] = "usual",
                    ["system"] = "urn:oid:1.2.840.114350.1.13.93.3.7.5.737384.0",
                    ["type"] = new Dictionary<string, object> { ["text"] = "EPI" },
                    ["value"] = epi
                },
                new Dictionary<string, object>
                {
                    ["use"] = "usual",
                    ["system"] = "http://open.epic.com/FHIR/StructureDefinition/patient-fhir-id",
                    ["type"] = new Dictionary<string, object> { ["text"] = "FHIR STU3" },
                    ["value"] = id
                }
            },
            ["name"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["use"] = "usual",
                    ["family"] = familyName,
                    ["given"] = givenNames,
                    ["text"] = $"{string.Join(" ", givenNames)} {familyName}"
                }
            },
            ["telecom"] = new object[]
            {
                new Dictionary<string, object> { ["system"] = "phone", ["value"] = phone, ["use"] = "home" }
            },
            ["gender"] = gender,
            ["birthDate"] = birthDate,
            ["active"] = true,
            ["deceasedBoolean"] = false,
            ["address"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["use"] = "home",
                    ["line"] = new[] { $"{(id.GetHashCode() & 0x3FF) + 1} Synthetic Lane" },
                    ["city"] = "TestCity",
                    ["state"] = "TX",
                    ["postalCode"] = "75001",
                    ["country"] = "US"
                }
            },
            ["contact"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["relationship"] = new object[]
                    {
                        CodeableConcept("http://terminology.hl7.org/CodeSystem/v2-0131", "C", "Emergency Contact")
                    },
                    ["name"] = new Dictionary<string, object>
                    {
                        ["use"] = "usual",
                        ["text"] = $"Emergency-Contact-{id}"
                    }
                }
            },
            ["communication"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["language"] = CodeableConcept("urn:ietf:bcp:47", "en", "English"),
                    ["preferred"] = true
                }
            }
        };
    }

    private static object MakeEncounter(string id, string patientId, DateTime start, DateTime end,
        string attendingPractId, string admittingPractId, string careTeamId, int index)
    {
        var reasonVariant = ConditionVariants[index % ConditionVariants.Length];
        var encType = index % 3 == 0
            ? ("183452005", "Emergency hospital admission (procedure)")
            : ("32485007",  "Hospital admission (procedure)");

        return new Dictionary<string, object>
        {
            ["resourceType"] = "Encounter",
            ["id"] = id,
            ["status"] = "finished",
            ["class"] = new Dictionary<string, object>
            {
                ["system"] = "http://terminology.hl7.org/CodeSystem/v3-ActCode",
                ["code"] = "IMP",
                ["display"] = "inpatient encounter"
            },
            ["type"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["coding"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["system"] = "http://snomed.info/sct",
                            ["code"] = encType.Item1,
                            ["display"] = encType.Item2
                        }
                    },
                    ["text"] = encType.Item2
                }
            },
            ["serviceType"] = new Dictionary<string, object>
            {
                ["coding"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["system"] = "http://terminology.hl7.org/CodeSystem/service-type",
                        ["code"] = "320",
                        ["display"] = "Dieticians"
                    }
                },
                ["text"] = "Inpatient"
            },
            ["priority"] = CodeableConcept("http://terminology.hl7.org/CodeSystem/v3-ActPriority",
                index % 3 == 0 ? "EM" : "R",
                index % 3 == 0 ? "emergency" : "routine"),
            ["subject"] = new Dictionary<string, object>
            {
                ["reference"] = $"Patient/{patientId}",
                ["display"] = $"Synthetic Patient {patientId}"
            },
            ["participant"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["coding"] = new object[]
                            {
                                new Dictionary<string, object>
                                {
                                    ["system"] = "http://hl7.org/fhir/v3/ParticipationType",
                                    ["code"] = "ATND",
                                    ["display"] = "attender"
                                }
                            },
                            ["text"] = "attender"
                        }
                    },
                    ["individual"] = new Dictionary<string, object>
                    {
                        ["reference"] = $"Practitioner/{attendingPractId}",
                        ["display"] = "Attending Physician",
                        ["type"] = "Practitioner"
                    }
                },
                new Dictionary<string, object>
                {
                    ["type"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["coding"] = new object[]
                            {
                                new Dictionary<string, object>
                                {
                                    ["system"] = "http://hl7.org/fhir/v3/ParticipationType",
                                    ["code"] = "ADM",
                                    ["display"] = "admitter"
                                }
                            },
                            ["text"] = "admitter"
                        }
                    },
                    ["individual"] = new Dictionary<string, object>
                    {
                        ["reference"] = $"Practitioner/{admittingPractId}",
                        ["display"] = "Admitting Physician",
                        ["type"] = "Practitioner"
                    }
                }
            },
            ["period"] = new Dictionary<string, object>
            {
                ["start"] = start.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["end"]   = end.ToString("yyyy-MM-ddTHH:mm:ssZ")
            },
            ["length"] = new Dictionary<string, object>
            {
                ["value"] = (end - start).TotalDays,
                ["unit"] = "d",
                ["system"] = "http://unitsofmeasure.org",
                ["code"] = "d"
            },
            ["reasonCode"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["coding"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["system"] = "http://snomed.info/sct",
                            ["code"] = reasonVariant.Code,
                            ["display"] = reasonVariant.Display
                        },
                        new Dictionary<string, object>
                        {
                            ["system"] = "http://hl7.org/fhir/sid/icd-10-cm",
                            ["code"] = reasonVariant.IcdCode,
                            ["display"] = reasonVariant.Display
                        }
                    },
                    ["text"] = reasonVariant.Display
                }
            },
            ["hospitalization"] = new Dictionary<string, object>
            {
                ["admitSource"] = CodeableConcept("http://terminology.hl7.org/CodeSystem/admit-source",
                    index % 2 == 0 ? "emd" : "hosp",
                    index % 2 == 0 ? "From accident/emergency department" : "Transferred from other hospital"),
                ["dischargeDisposition"] = CodeableConcept("http://terminology.hl7.org/CodeSystem/discharge-disposition",
                    "home", "Home")
            },
            ["location"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["location"] = Ref($"Location/{EdLocationId}"),
                    ["status"] = "completed",
                    ["period"] = new Dictionary<string, object>
                    {
                        ["start"] = start.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        ["end"]   = start.AddHours(6).ToString("yyyy-MM-ddTHH:mm:ssZ")
                    }
                },
                new Dictionary<string, object>
                {
                    ["location"] = Ref($"Location/{IcuLocationId}"),
                    ["status"] = "completed",
                    ["period"] = new Dictionary<string, object>
                    {
                        ["start"] = start.AddHours(6).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        ["end"]   = end.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ")
                    }
                },
                new Dictionary<string, object>
                {
                    ["location"] = Ref($"Location/{StepDownLocationId}"),
                    ["status"] = "completed",
                    ["period"] = new Dictionary<string, object>
                    {
                        ["start"] = end.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        ["end"]   = end.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    }
                }
            },
            ["serviceProvider"] = Ref($"Organization/{HospitalOrgId}")
        };
    }

    private static object MakeObservation(string id, string patientId, string encounterId,
        DateTime effective, int index, List<string> specimenIds, List<string> observationIds)
    {
        var variant = ObservationVariants[index % ObservationVariants.Length];
        observationIds.Add(id);

        // Lab observations get effectivePeriod + specimen reference; vitals get effectiveDateTime
        var isLab = variant.Category == "laboratory";
        var periodEnd = effective.AddHours(1 + (index % 4));

        var obs = new Dictionary<string, object>
        {
            ["resourceType"] = "Observation",
            ["id"] = id,
            ["status"] = "final",
            ["category"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["coding"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["system"] = "http://terminology.hl7.org/CodeSystem/observation-category",
                            ["code"] = variant.Category,
                            ["display"] = isLab ? "Laboratory" : "Vital Signs"
                        }
                    }
                }
            },
            ["code"] = LoincConcept(variant.Code, variant.Display),
            ["subject"] = new Dictionary<string, object>
            {
                ["reference"] = $"Patient/{patientId}",
                ["display"] = $"Synthetic Patient {patientId}"
            },
            ["encounter"] = new Dictionary<string, object>
            {
                ["reference"] = $"Encounter/{encounterId}",
                ["display"] = "Hospital Encounter"
            },
        };

        if (isLab)
        {
            obs["effectivePeriod"] = new Dictionary<string, object>
            {
                ["start"] = effective.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["end"]   = periodEnd.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
            if (specimenIds.Count > 0)
            {
                obs["specimen"] = new Dictionary<string, object>
                {
                    ["reference"] = $"Specimen/{specimenIds[index % specimenIds.Count]}",
                    ["display"] = "Specimen"
                };
            }
        }
        else
        {
            obs["effectiveDateTime"] = effective.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        // Numeric value with UCUM unit (except culture which is text)
        if (variant.Code == "600-7")
        {
            obs["valueString"] = index % 2 == 0 ? "No growth" : "Staphylococcus aureus";
        }
        else if (variant.Code == "55284-4")
        {
            // Blood pressure — component-based
            obs["component"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["code"] = LoincConcept("8480-6", "Systolic blood pressure"),
                    ["valueQuantity"] = Quantity(100 + (index % 80), "mm[Hg]", "mm[Hg]")
                },
                new Dictionary<string, object>
                {
                    ["code"] = LoincConcept("8462-4", "Diastolic blood pressure"),
                    ["valueQuantity"] = Quantity(60 + (index % 40), "mm[Hg]", "mm[Hg]")
                }
            };
        }
        else
        {
            var value = variant.Low + ((index % 100) / 100.0) * (variant.High - variant.Low);
            obs["valueQuantity"] = Quantity(Math.Round(value, 1), variant.Unit, variant.Unit);

            if (variant.Low > 0 || variant.High > 0)
            {
                obs["referenceRange"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["low"]  = Quantity(variant.Low, variant.Unit, variant.Unit),
                        ["high"] = Quantity(variant.High, variant.Unit, variant.Unit),
                        ["text"] = $"{variant.Low} - {variant.High} {variant.Unit}"
                    }
                };
            }
        }

        return obs;
    }

    private static object MakeCondition(string id, string patientId, string encounterId,
        DateTime onset, DateTime abatement, int index)
    {
        var variant = ConditionVariants[index % ConditionVariants.Length];
        var isActive = index % 4 != 0;

        var condition = new Dictionary<string, object>
        {
            ["resourceType"] = "Condition",
            ["id"] = id,
            ["clinicalStatus"] = CodeableConcept(
                "http://terminology.hl7.org/CodeSystem/condition-clinical",
                isActive ? "active" : "resolved",
                isActive ? "Active" : "Resolved"),
            ["verificationStatus"] = CodeableConcept(
                "http://terminology.hl7.org/CodeSystem/condition-ver-status",
                "confirmed", "Confirmed"),
            ["category"] = new object[]
            {
                CodeableConcept("http://terminology.hl7.org/CodeSystem/condition-category",
                    "encounter-diagnosis", "Encounter Diagnosis")
            },
            ["severity"] = CodeableConcept("http://snomed.info/sct",
                index % 3 == 0 ? "24484000" : index % 3 == 1 ? "6736007" : "255604002",
                index % 3 == 0 ? "Severe" : index % 3 == 1 ? "Moderate" : "Mild"),
            ["code"] = new Dictionary<string, object>
            {
                ["coding"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["system"] = "http://snomed.info/sct",
                        ["code"] = variant.Code,
                        ["display"] = variant.Display
                    },
                    new Dictionary<string, object>
                    {
                        ["system"] = "http://hl7.org/fhir/sid/icd-10-cm",
                        ["code"] = variant.IcdCode,
                        ["display"] = variant.Display
                    }
                },
                ["text"] = variant.Display
            },
            ["subject"] = Ref($"Patient/{patientId}"),
            ["encounter"] = Ref($"Encounter/{encounterId}"),
            ["onsetDateTime"] = onset.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["recordedDate"] = onset.ToString("yyyy-MM-dd")
        };

        if (!isActive)
        {
            condition["abatementDateTime"] = abatement.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        return condition;
    }

    private static object MakeProcedure(string id, string patientId, string encounterId,
        DateTime performed, int index, string practId)
    {
        var variant = ProcedureVariants[index % ProcedureVariants.Length];
        var duration = TimeSpan.FromMinutes(30 + (index % 180));

        return new Dictionary<string, object>
        {
            ["resourceType"] = "Procedure",
            ["id"] = id,
            ["status"] = "completed",
            ["code"] = SnomedConcept(variant.Code, variant.Display),
            ["subject"] = Ref($"Patient/{patientId}"),
            ["encounter"] = Ref($"Encounter/{encounterId}"),
            ["performedPeriod"] = new Dictionary<string, object>
            {
                ["start"] = performed.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["end"]   = performed.Add(duration).ToString("yyyy-MM-ddTHH:mm:ssZ")
            },
            ["performer"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["actor"] = new Dictionary<string, object>
                    {
                        ["reference"] = $"Practitioner/{practId}",
                        ["display"] = "Attending Physician",
                        ["type"] = "Practitioner"
                    },
                    ["onBehalfOf"] = Ref($"Organization/{HospitalOrgId}")
                }
            },
            ["location"] = Ref($"Location/{HospitalLocationId}"),
            ["reasonCode"] = new object[]
            {
                SnomedConcept(ConditionVariants[index % ConditionVariants.Length].Code,
                              ConditionVariants[index % ConditionVariants.Length].Display)
            }
        };
    }

    private static object MakeMedicationRequest(string id, string patientId, string encounterId,
        DateTime authored, int index, string practId)
    {
        var variant = MedicationVariants[index % MedicationVariants.Length];

        return new Dictionary<string, object>
        {
            ["resourceType"] = "MedicationRequest",
            ["id"] = id,
            ["status"] = index % 5 == 0 ? "completed" : "active",
            ["intent"] = "order",
            ["medicationCodeableConcept"] = RxNormConcept(variant.RxCode, variant.Display),
            ["subject"] = Ref($"Patient/{patientId}"),
            ["encounter"] = Ref($"Encounter/{encounterId}"),
            ["authoredOn"] = authored.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["requester"] = new Dictionary<string, object>
            {
                ["reference"] = $"Practitioner/{practId}",
                ["display"] = "Ordering Physician",
                ["type"] = "Practitioner"
            },
            ["dosageInstruction"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["text"] = $"{variant.DoseValue} {variant.DoseUnit} {variant.RouteDisplay}",
                    ["timing"] = new Dictionary<string, object>
                    {
                        ["repeat"] = new Dictionary<string, object>
                        {
                            ["frequency"] = 1 + (index % 3),
                            ["period"] = 1,
                            ["periodUnit"] = "d"
                        }
                    },
                    ["route"] = SnomedConcept(variant.RouteCode, variant.RouteDisplay),
                    ["doseAndRate"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["type"] = CodeableConcept("http://terminology.hl7.org/CodeSystem/dose-rate-type",
                                "ordered", "Ordered"),
                            ["doseQuantity"] = Quantity(variant.DoseValue, variant.DoseUnit, variant.DoseUnit)
                        }
                    }
                }
            }
        };
    }

    private static object MakeMedicationAdministration(string id, string patientId, string encounterId,
        DateTime effective, int index, List<string> medicationIds, string practId)
    {
        var variant = MedicationVariants[index % MedicationVariants.Length];

        var admin = new Dictionary<string, object>
        {
            ["resourceType"] = "MedicationAdministration",
            ["id"] = id,
            ["status"] = "completed",
            ["category"] = CodeableConcept(
                "http://terminology.hl7.org/CodeSystem/medication-admin-location",
                "inpatient", "Inpatient"),
            ["subject"] = Ref($"Patient/{patientId}"),
            ["context"] = Ref($"Encounter/{encounterId}"),
            ["effectiveDateTime"] = effective.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["performer"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["actor"] = new Dictionary<string, object>
                    {
                        ["reference"] = $"Practitioner/{practId}",
                        ["display"] = "Administering Nurse",
                        ["type"] = "Practitioner"
                    }
                }
            },
            ["dosage"] = new Dictionary<string, object>
            {
                ["route"] = SnomedConcept(variant.RouteCode, variant.RouteDisplay),
                ["dose"]  = Quantity(variant.DoseValue, variant.DoseUnit, variant.DoseUnit)
            }
        };

        if (medicationIds.Count > 0)
        {
            var medRef = medicationIds[index % medicationIds.Count];
            admin["medicationReference"] = new Dictionary<string, object>
            {
                ["reference"] = $"Medication/{medRef}",
                ["display"] = variant.Display,
                ["extension"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["url"] = "https://www.cdc.gov/nhsn/fhir/nhsnlink/StructureDefinition/nhsnlink-original-id",
                        ["valueString"] = $"Medication/{medRef}"
                    }
                }
            };
        }
        else
        {
            admin["medicationCodeableConcept"] = RxNormConcept(variant.RxCode, variant.Display);
        }

        return admin;
    }

    private static object MakeMedication(string id, int index, List<string> medicationIds)
    {
        var variant = MedicationVariants[index % MedicationVariants.Length];
        medicationIds.Add(id);

        return new Dictionary<string, object>
        {
            ["resourceType"] = "Medication",
            ["id"] = id,
            ["code"] = RxNormConcept(variant.RxCode, variant.Display),
            ["status"] = "active",
            ["form"] = CodeableConcept("http://snomed.info/sct",
                variant.DoseUnit == "mg" ? "385055001" : "385219001",
                variant.DoseUnit == "mg" ? "Tablet" : "Injectable solution"),
            ["ingredient"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["itemCodeableConcept"] = RxNormConcept(variant.RxCode, variant.Display),
                    ["strength"] = new Dictionary<string, object>
                    {
                        ["numerator"]   = Quantity(variant.DoseValue, variant.DoseUnit, variant.DoseUnit),
                        ["denominator"] = Quantity(1, "1", "1")
                    }
                }
            }
        };
    }

    private static object MakeDiagnosticReport(string id, string patientId, string encounterId,
        DateTime effective, int index, List<string> observationIds, string practId)
    {
        var labReports = new[]
        {
            ("58410-2", "CBC panel - Blood by Automated count",    "LAB"),
            ("24323-8", "Comprehensive metabolic panel - Serum",   "LAB"),
            ("24331-1", "Lipid panel - Serum or Plasma",           "LAB"),
            ("57698-3", "Lipid panel with direct LDL",             "LAB"),
            ("24357-6", "Urinalysis panel",                        "LAB"),
            ("47519-4", "History of Procedures Document",          "LP29684-5"),
            ("11488-4", "Consult Note",                            "LP29684-5"),
        };

        var rpt = labReports[index % labReports.Length];
        var resultRefs = observationIds.Count > 0
            ? observationIds.Skip(index % Math.Max(observationIds.Count, 1))
                            .Take(3)
                            .Select(oid => (object)Ref($"Observation/{oid}"))
                            .ToArray()
            : Array.Empty<object>();

        var category = new object[]
        {
            new Dictionary<string, object>
            {
                ["coding"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["system"]  = "http://terminology.hl7.org/CodeSystem/v2-0074",
                        ["code"]    = rpt.Item3,
                        ["display"] = "Laboratory"
                    }
                }
            }
        };

        var report = new Dictionary<string, object>
        {
            ["resourceType"]     = "DiagnosticReport",
            ["id"]               = id,
            ["status"]           = "final",
            ["category"]         = category,
            ["code"]             = LoincConcept(rpt.Item1, rpt.Item2),
            ["subject"]          = Ref($"Patient/{patientId}"),
            ["encounter"]        = Ref($"Encounter/{encounterId}"),
            ["effectiveDateTime"] = effective.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["issued"]           = effective.AddHours(2).ToString("yyyy-MM-ddTHH:mm:sszzz"),
            ["performer"]        = new object[]
            {
                new Dictionary<string, object>
                {
                    ["reference"] = $"Practitioner/{practId}",
                    ["display"]   = "Interpreting Physician",
                    ["type"]      = "Practitioner"
                }
            }
        };

        if (resultRefs.Length > 0)
        {
            report["result"] = resultRefs;
        }

        return report;
    }

    private static object MakeServiceRequest(string id, string patientId, string encounterId,
        DateTime authored, int index, string practId)
    {
        var variant = ServiceRequestVariants[index % ServiceRequestVariants.Length];
        var isLab = index % 2 == 0;

        return new Dictionary<string, object>
        {
            ["resourceType"] = "ServiceRequest",
            ["id"] = id,
            ["status"] = index % 4 == 0 ? "completed" : "active",
            ["intent"] = "order",
            ["category"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["coding"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["system"] = "http://snomed.info/sct",
                            ["code"] = isLab ? "108252007" : "409073007",
                            ["display"] = isLab ? "Laboratory procedure" : "Education"
                        }
                    }
                }
            },
            ["code"] = isLab
                ? LoincConcept(variant.Code, variant.Display)
                : SnomedConcept(variant.Code, variant.Display),
            ["subject"] = new Dictionary<string, object>
            {
                ["reference"] = $"Patient/{patientId}",
                ["display"] = $"Synthetic Patient {patientId}"
            },
            ["encounter"] = new Dictionary<string, object>
            {
                ["reference"] = $"Encounter/{encounterId}",
                ["display"] = "Hospital Encounter"
            },
            ["authoredOn"] = authored.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["requester"] = new Dictionary<string, object>
            {
                ["reference"] = $"Practitioner/{practId}",
                ["display"] = "Ordering Physician",
                ["type"] = "Practitioner"
            }
        };
    }

    private static object MakeCoverage(string id, string patientId, DateTime start, DateTime end, int index)
    {
        var payors = new[]
        {
            ("Medicare",  "1-800-MEDICARE",  "MC"),
            ("Medicaid",  "1-800-MEDICAID",  "MD"),
            ("BlueCross", "1-800-BCBS",      "BC"),
            ("Aetna",     "1-800-AETNA",     "AE"),
        };
        var payor = payors[index % payors.Length];

        return new Dictionary<string, object>
        {
            ["resourceType"] = "Coverage",
            ["id"] = id,
            ["status"] = "active",
            ["type"] = CodeableConcept("http://terminology.hl7.org/CodeSystem/v3-ActCode",
                payor.Item3, payor.Item1),
            ["subscriberId"] = $"SUB-{id.GetHashCode() & 0xFFFFFF:X6}",
            ["beneficiary"] = Ref($"Patient/{patientId}"),
            ["relationship"] = CodeableConcept("http://terminology.hl7.org/CodeSystem/subscriber-relationship",
                "self", "Self"),
            ["period"] = new Dictionary<string, object>
            {
                ["start"] = start.ToString("yyyy-MM-dd"),
                ["end"]   = end.AddYears(1).ToString("yyyy-MM-dd")
            },
            ["payor"] = new object[] { Ref($"Patient/{patientId}") },
            ["class"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = CodeableConcept("http://terminology.hl7.org/CodeSystem/coverage-class",
                        "plan", "Plan"),
                    ["value"] = $"{payor.Item3}-PLAN-{index % 5 + 1:D3}",
                    ["name"] = $"{payor.Item1} Plan {index % 5 + 1}"
                }
            }
        };
    }

    private static object MakeSpecimen(string id, string patientId, DateTime collected, int index,
        List<string> specimenIds)
    {
        specimenIds.Add(id);
        var variant = SpecimenVariants[index % SpecimenVariants.Length];
        var volumeMl = 2.0 + (index % 8);

        return new Dictionary<string, object>
        {
            ["resourceType"] = "Specimen",
            ["id"] = id,
            ["status"] = "available",
            ["type"] = new Dictionary<string, object>
            {
                ["coding"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["system"] = variant.System,
                        ["code"]    = variant.Code,
                        ["display"] = variant.Display
                    }
                }
            },
            ["subject"] = Ref($"Patient/{patientId}"),
            ["collection"] = new Dictionary<string, object>
            {
                ["collectedDateTime"] = collected.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["quantity"] = new Dictionary<string, object>
                {
                    ["value"] = volumeMl,
                    ["unit"] = "mL",
                    ["system"] = "http://unitsofmeasure.org",
                    ["code"] = "mL"
                }
            }
        };
    }

    private static object MakeAllergyIntolerance(string id, string patientId, DateTime recorded, int index)
    {
        var variant = AllergyVariants[index % AllergyVariants.Length];
        var criticality = new[] { "low", "high", "unable-to-assess" };
        var category = new[] { "environment", "medication", "food", "biologic" };

        return new Dictionary<string, object>
        {
            ["resourceType"] = "AllergyIntolerance",
            ["id"] = id,
            ["clinicalStatus"] = CodeableConcept(
                "http://terminology.hl7.org/CodeSystem/allergyintolerance-clinical",
                "active", "Active"),
            ["verificationStatus"] = CodeableConcept(
                "http://terminology.hl7.org/CodeSystem/allergyintolerance-verification",
                "confirmed", "Confirmed"),
            ["type"] = index % 2 == 0 ? "allergy" : "intolerance",
            ["category"] = new[] { category[index % category.Length] },
            ["criticality"] = criticality[index % criticality.Length],
            ["code"] = new Dictionary<string, object>
            {
                ["coding"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["system"] = "http://snomed.info/sct",
                        ["code"] = variant.Code,
                        ["display"] = variant.Display
                    }
                },
                ["text"] = variant.Display
            },
            ["patient"] = Ref($"Patient/{patientId}"),
            ["recordedDate"] = recorded.ToString("yyyy-MM-dd"),
            ["reaction"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["manifestation"] = new object[]
                    {
                        SnomedConcept(
                            index % 3 == 0 ? "271807003" : index % 3 == 1 ? "267036007" : "25064002",
                            index % 3 == 0 ? "Eruption of skin" : index % 3 == 1 ? "Dyspnoea" : "Headache")
                    },
                    ["severity"] = index % 3 == 0 ? "mild" : index % 3 == 1 ? "moderate" : "severe"
                }
            }
        };
    }

    private static object MakeImmunization(string id, string patientId, string encounterId,
        DateTime occurrence, int index)
    {
        var variant = ImmunizationVariants[index % ImmunizationVariants.Length];
        var lotNumber = $"LOT{(index * 7 + 1001):D5}";

        return new Dictionary<string, object>
        {
            ["resourceType"] = "Immunization",
            ["id"] = id,
            ["status"] = "completed",
            ["vaccineCode"] = new Dictionary<string, object>
            {
                ["coding"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["system"] = "http://hl7.org/fhir/sid/cvx",
                        ["code"] = variant.CvxCode,
                        ["display"] = variant.Display
                    }
                },
                ["text"] = variant.Display
            },
            ["patient"] = Ref($"Patient/{patientId}"),
            ["encounter"] = Ref($"Encounter/{encounterId}"),
            ["occurrenceDateTime"] = occurrence.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["primarySource"] = true,
            ["lotNumber"] = lotNumber,
            ["location"] = Ref($"Location/{HospitalLocationId}"),
            ["doseQuantity"] = Quantity(0.5, "mL", "mL")
        };
    }

    private static object MakeImagingStudy(string id, string patientId, string encounterId,
        DateTime started, int index)
    {
        var variant = ImagingVariants[index % ImagingVariants.Length];
        var bodyParts = variant.BodySite.Split('|');
        var studyUid = $"1.2.840.99999.{Math.Abs(id.GetHashCode())}.{index + 1}";
        var seriesUid = $"{studyUid}.1";
        var instanceUid = $"{seriesUid}.1";

        return new Dictionary<string, object>
        {
            ["resourceType"] = "ImagingStudy",
            ["id"] = id,
            ["identifier"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["use"] = "official",
                    ["system"] = "urn:ietf:rfc:3986",
                    ["value"] = $"urn:oid:{studyUid}"
                }
            },
            ["status"] = "available",
            ["subject"] = Ref($"Patient/{patientId}"),
            ["encounter"] = Ref($"Encounter/{encounterId}"),
            ["started"] = started.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            ["numberOfSeries"] = 1,
            ["numberOfInstances"] = 1,
            ["procedureCode"] = new object[]
            {
                SnomedConcept(variant.SnomedCode, variant.Display)
            },
            ["location"] = Ref($"Location/{HospitalLocationId}"),
            ["series"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["uid"] = seriesUid,
                    ["number"] = 1,
                    ["modality"] = new Dictionary<string, object>
                    {
                        ["system"] = "http://dicom.nema.org/medical/dicom/current/output/chtml/part16/sect_CID_29.html",
                        ["code"] = variant.Modality,
                        ["display"] = variant.Modality
                    },
                    ["numberOfInstances"] = 1,
                    ["bodySite"] = new Dictionary<string, object>
                    {
                        ["system"] = "http://snomed.info/sct",
                        ["code"] = bodyParts[0],
                        ["display"] = bodyParts.Length > 1 ? bodyParts[1] : bodyParts[0]
                    },
                    ["started"] = started.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    ["instance"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["uid"] = instanceUid,
                            ["sopClass"] = new Dictionary<string, object>
                            {
                                ["system"] = "urn:ietf:rfc:3986",
                                ["code"] = "urn:oid:1.2.840.10008.5.1.4.1.1.3.1"
                            },
                            ["number"] = 1,
                            ["title"] = "Image Storage"
                        }
                    }
                }
            }
        };
    }

    private static object MakeCareTeam(string id, string patientId, string encounterId,
        string attendingPractId, DateTime period)
    {
        return new Dictionary<string, object>
        {
            ["resourceType"] = "CareTeam",
            ["id"] = id,
            ["status"] = "active",
            ["name"] = $"Inpatient Care Team - {patientId}",
            ["subject"] = Ref($"Patient/{patientId}"),
            ["encounter"] = Ref($"Encounter/{encounterId}"),
            ["period"] = new Dictionary<string, object>
            {
                ["start"] = period.ToString("yyyy-MM-ddTHH:mm:ssZ")
            },
            ["participant"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["role"] = new object[]
                    {
                        CodeableConcept("http://snomed.info/sct", "17561000", "Cardiologist")
                    },
                    ["member"] = new Dictionary<string, object>
                    {
                        ["reference"] = $"Practitioner/{attendingPractId}",
                        ["display"] = "Attending Physician",
                        ["type"] = "Practitioner"
                    }
                }
            },
            ["managingOrganization"] = new object[] { Ref($"Organization/{HospitalOrgId}") }
        };
    }

    private static object MakeCarePlan(string id, string patientId, string encounterId,
        string careTeamId, DateTime period, int index)
    {
        var activities = new[]
        {
            ("384758001", "Self-care interventions (procedure)"),
            ("229070002", "Dietary education (procedure)"),
            ("103735009", "Palliative care (regime/therapy)"),
            ("40701008",  "Echocardiography (procedure)"),
            ("182804009", "Drug compliance therapy (regime/therapy)"),
        };
        var act = activities[index % activities.Length];

        return new Dictionary<string, object>
        {
            ["resourceType"] = "CarePlan",
            ["id"] = id,
            ["status"] = "active",
            ["intent"] = "order",
            ["category"] = new object[]
            {
                CodeableConcept("http://hl7.org/fhir/us/core/CodeSystem/careplan-category",
                    "assess-plan", "Assessment and Plan of Treatment"),
                SnomedConcept(act.Item1, act.Item2)
            },
            ["subject"] = Ref($"Patient/{patientId}"),
            ["encounter"] = Ref($"Encounter/{encounterId}"),
            ["period"] = new Dictionary<string, object>
            {
                ["start"] = period.ToString("yyyy-MM-ddTHH:mm:ssZ")
            },
            ["careTeam"] = new object[] { Ref($"CareTeam/{careTeamId}") },
            ["activity"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["detail"] = new Dictionary<string, object>
                    {
                        ["code"] = SnomedConcept(act.Item1, act.Item2),
                        ["status"] = "in-progress",
                        ["location"] = new Dictionary<string, object>
                        {
                            ["display"] = "General Test Hospital"
                        }
                    }
                }
            }
        };
    }

    private static object MakeDocumentReference(string id, string patientId, string encounterId,
        DateTime created, int index)
    {
        var docTypes = new[]
        {
            ("11488-4",  "Consult Note"),
            ("18842-5",  "Discharge Summary"),
            ("34117-2",  "History & Physical Note"),
            ("11506-3",  "Progress Note"),
            ("57133-1",  "Referral Note"),
        };
        var doc = docTypes[index % docTypes.Length];
        var contentText = $"Clinical note for patient {patientId} - {doc.Item2} - {created:yyyy-MM-dd}";
        var contentB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(contentText));

        return new Dictionary<string, object>
        {
            ["resourceType"] = "DocumentReference",
            ["id"] = id,
            ["status"] = "current",
            ["type"] = LoincConcept(doc.Item1, doc.Item2),
            ["category"] = new object[]
            {
                LoincConcept(doc.Item1, doc.Item2)
            },
            ["subject"] = Ref($"Patient/{patientId}"),
            ["date"] = created.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            ["author"] = new object[] { Ref($"Organization/{HospitalOrgId}") },
            ["custodian"] = Ref($"Organization/{HospitalOrgId}"),
            ["content"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["attachment"] = new Dictionary<string, object>
                    {
                        ["contentType"] = "text/plain; charset=utf-8",
                        ["data"] = contentB64
                    },
                    ["format"] = new Dictionary<string, object>
                    {
                        ["system"] = "http://ihe.net/fhir/ihe.formatcode.fhir/CodeSystem/formatcode",
                        ["code"] = "urn:ihe:iti:xds:2017:mimeTypeSufficient",
                        ["display"] = "mimeType Sufficient"
                    }
                }
            },
            ["context"] = new Dictionary<string, object>
            {
                ["encounter"] = new object[] { Ref($"Encounter/{encounterId}") },
                ["period"] = new Dictionary<string, object>
                {
                    ["start"] = created.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ["end"]   = created.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ssZ")
                }
            }
        };
    }

    private static object MakeProvenance(string id, string patientId, string encounterId,
        DateTime recorded, string practId)
    {
        return new Dictionary<string, object>
        {
            ["resourceType"] = "Provenance",
            ["id"] = id,
            ["target"] = new object[] { Ref($"Encounter/{encounterId}") },
            ["recorded"] = recorded.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            ["agent"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = CodeableConcept(
                        "http://terminology.hl7.org/CodeSystem/provenance-participant-type",
                        "author", "Author"),
                    ["who"] = new Dictionary<string, object>
                    {
                        ["reference"] = $"Practitioner/{practId}",
                        ["display"] = "Authoring Clinician"
                    },
                    ["onBehalfOf"] = Ref($"Organization/{HospitalOrgId}")
                },
                new Dictionary<string, object>
                {
                    ["type"] = CodeableConcept(
                        "http://hl7.org/fhir/us/core/CodeSystem/us-core-provenance-participant-type",
                        "transmitter", "Transmitter"),
                    ["who"] = new Dictionary<string, object>
                    {
                        ["reference"] = $"Practitioner/{practId}",
                        ["display"] = "Authoring Clinician"
                    },
                    ["onBehalfOf"] = Ref($"Organization/{HospitalOrgId}")
                }
            }
        };
    }

    private static object MakeCensusList(string id, string patientId, string listPrefix, DateTime date)
    {
        return new Dictionary<string, object>
        {
            ["resourceType"] = "List",
            ["id"] = id,
            ["status"] = "current",
            ["mode"] = "working",
            ["title"] = $"Synthetic Census List - {listPrefix} - {patientId}",
            ["code"] = new Dictionary<string, object>
            {
                ["coding"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["system"] = "http://hl7.org/fhir/list-example-use-codes",
                        ["code"] = "patients",
                        ["display"] = "Patient List"
                    }
                },
                ["text"] = "Patient List"
            },
            ["date"] = date.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            ["entry"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["item"] = new Dictionary<string, object>
                    {
                        ["reference"] = $"Patient/{patientId}",
                        ["display"] = $"Synthetic Patient {patientId}"
                    }
                }
            }
        };
    }

    private static object MakePractitioner(string id, string family, string given, string gender, string email)
    {
        return new Dictionary<string, object>
        {
            ["resourceType"] = "Practitioner",
            ["id"] = id,
            ["meta"] = new Dictionary<string, object>
            {
                ["profile"] = new[] { "http://hl7.org/fhir/us/core/StructureDefinition/us-core-practitioner" }
            },
            ["identifier"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["system"] = "http://hl7.org/fhir/sid/us-npi",
                    ["value"] = $"{Math.Abs(id.GetHashCode()) % 9_000_000_000L + 1_000_000_000L}"
                }
            },
            ["active"] = true,
            ["name"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["family"] = family,
                    ["given"] = new[] { given },
                    ["prefix"] = new[] { "Dr." }
                }
            },
            ["telecom"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["system"] = "email",
                    ["value"] = email,
                    ["use"] = "work",
                    ["extension"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["url"] = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-direct",
                            ["valueBoolean"] = true
                        }
                    }
                }
            },
            ["address"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["line"] = new[] { "100 Hospital Drive" },
                    ["city"] = "TestCity",
                    ["state"] = "TX",
                    ["postalCode"] = "75001",
                    ["country"] = "US"
                }
            },
            ["gender"] = gender
        };
    }

    private static object MakeOrganization(string id, string name, string alias)
    {
        return new Dictionary<string, object>
        {
            ["resourceType"] = "Organization",
            ["id"] = id,
            ["meta"] = new Dictionary<string, object>
            {
                ["profile"] = new[] { "http://hl7.org/fhir/us/core/StructureDefinition/us-core-organization" }
            },
            ["identifier"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["system"] = "https://github.com/synthetichealth/synthea",
                    ["value"] = id
                }
            },
            ["active"] = true,
            ["type"] = new object[]
            {
                CodeableConcept("http://terminology.hl7.org/CodeSystem/organization-type",
                    "prov", "Healthcare Provider")
            },
            ["name"] = name,
            ["alias"] = new[] { alias },
            ["telecom"] = new object[]
            {
                new Dictionary<string, object> { ["system"] = "phone", ["value"] = "555-867-5309" }
            },
            ["address"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["line"] = new[] { "100 Hospital Drive" },
                    ["city"] = "TestCity",
                    ["state"] = "TX",
                    ["postalCode"] = "75001",
                    ["country"] = "US"
                }
            }
        };
    }

    private static object MakeLocation(string id, string typeCode, string name, string managingOrgId)
    {
        return new Dictionary<string, object>
        {
            ["resourceType"] = "Location",
            ["id"] = id,
            ["status"] = "active",
            ["name"] = name,
            ["identifier"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["system"] = "http://example.org/fhir/sid/location",
                    ["value"] = id
                }
            },
            ["type"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["coding"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["system"] = "http://terminology.hl7.org/CodeSystem/v3-RoleCode",
                            ["code"] = typeCode,
                            ["display"] = name
                        }
                    }
                }
            },
            ["managingOrganization"] = Ref($"Organization/{managingOrgId}"),
            ["physicalType"] = CodeableConcept(
                "http://terminology.hl7.org/CodeSystem/location-physical-type",
                "wa", "Ward")
        };
    }

    private static object MakeDevice(string id, string snomedCode, string display, string? patientId)
    {
        var device = new Dictionary<string, object>
        {
            ["resourceType"] = "Device",
            ["id"] = id,
            ["status"] = "active",
            ["type"] = SnomedConcept(snomedCode, display),
            ["deviceName"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["name"] = display,
                    ["type"] = "user-friendly-name"
                }
            },
            ["manufacturer"] = "SyntheticMed Devices Inc.",
            ["serialNumber"] = $"SN-{Math.Abs(id.GetHashCode()):X8}"
        };

        if (patientId != null)
        {
            device["patient"] = Ref($"Patient/{patientId}");
        }

        return device;
    }

    // ---------------------------------------------------------------
    //  Bundle entry wrapper
    // ---------------------------------------------------------------

    private static object MakeEntry(string fullUrl, object resource) => new Dictionary<string, object>
    {
        ["fullUrl"] = $"http://localhost:8080/fhir/{fullUrl}",
        ["resource"] = resource,
        ["request"] = new Dictionary<string, object>
        {
            ["method"] = "PUT",
            ["url"] = fullUrl
        }
    };

    // ---------------------------------------------------------------
    //  Concept / quantity helpers
    // ---------------------------------------------------------------

    private static Dictionary<string, object> Ref(string reference) =>
        new() { ["reference"] = reference };

    private static Dictionary<string, object> Quantity(double value, string unit, string code) =>
        new()
        {
            ["value"]  = value,
            ["unit"]   = unit,
            ["system"] = "http://unitsofmeasure.org",
            ["code"]   = code
        };

    private static Dictionary<string, object> LoincConcept(string code, string display) => new()
    {
        ["coding"] = new object[]
        {
            new Dictionary<string, object>
            {
                ["system"]  = "http://loinc.org",
                ["code"]    = code,
                ["display"] = display
            }
        },
        ["text"] = display
    };

    private static Dictionary<string, object> SnomedConcept(string code, string display) => new()
    {
        ["coding"] = new object[]
        {
            new Dictionary<string, object>
            {
                ["system"]  = "http://snomed.info/sct",
                ["code"]    = code,
                ["display"] = display
            }
        },
        ["text"] = display
    };

    private static Dictionary<string, object> RxNormConcept(string code, string display) => new()
    {
        ["coding"] = new object[]
        {
            new Dictionary<string, object>
            {
                ["system"]  = "http://www.nlm.nih.gov/research/umls/rxnorm",
                ["code"]    = code,
                ["display"] = display
            }
        },
        ["text"] = display
    };

    private static Dictionary<string, object> CodeableConcept(string system, string code, string? display = null)
    {
        var coding = new Dictionary<string, object>
        {
            ["system"] = system,
            ["code"]   = code
        };
        if (display != null) coding["display"] = display;

        var cc = new Dictionary<string, object>
        {
            ["coding"] = new object[] { coding }
        };
        if (display != null) cc["text"] = display;

        return cc;
    }
}
