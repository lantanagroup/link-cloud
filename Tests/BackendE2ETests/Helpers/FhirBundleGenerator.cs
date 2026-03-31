using System.Text.Json;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

/// <summary>
/// Generates synthetic FHIR transaction bundles for the MegaPatient stress test.
///
/// Each patient is built around a single inpatient Encounter that all clinical
/// resources reference, matching the pattern the NHSN Acute Care Hospital
/// (ACH) Initial Population measure expects:
///
///   Patient -> Encounter (IMP, Emergency hospital admission)
///                -> Location (hospital)
///                -> Condition (encounter-diagnosis)
///                -> Observation (vital-signs, laboratory)
///                -> Procedure
///                -> MedicationRequest -> Medication
///                -> MedicationAdministration
///                -> DiagnosticReport -> Specimen
///                -> ServiceRequest
///                -> Coverage
///                -> Device
///
/// Dates are anchored within the reporting period (2023-01-01 to 2023-12-31)
/// and all clinical resource dates fall within the encounter period.
///
/// Bundles are chunked to stay within FHIR server transaction size limits.
/// </summary>
public static class FhirBundleGenerator
{
    public const int DefaultPatientCount = 1;
    public const int DefaultResourcesPerPatient = 10_200;
    private const int MaxEntriesPerBundle = 500;

    // Shared Location and Device IDs (created once, referenced by all patients)
    private const string HospitalLocationId = "MegaTest-Location-Hospital";
    private const string IcuLocationId = "MegaTest-Location-ICU";
    private const string PulseOxDeviceId = "MegaTest-Device-PulseOx";
    private const string VentilatorDeviceId = "MegaTest-Device-Ventilator";

    // Encounter period per patient (offset by patient index to avoid identical data)
    private static DateTime EncounterStart(int patientIndex)
    {
        // Start at March 2023, cycle months, increment year as needed
        int baseYear = 2023;
        int baseMonth = 3;
        int monthOffset = patientIndex;
        int month = ((baseMonth - 1 + monthOffset) % 12) + 1;
        int year = baseYear + (baseMonth - 1 + monthOffset) / 12;
        return new DateTime(year, month, 1, 8, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime EncounterEnd(int patientIndex) =>
        EncounterStart(patientIndex).AddDays(10).AddHours(8);

    /// <summary>
    /// Resource distribution targeting ~10,200 per patient. Each resource is tied
    /// to the patient's single encounter.
    /// </summary>
    private static readonly (string ResourceType, double Fraction)[] ResourceDistribution =
    [
        ("Observation",             0.35),  // ~3,570  (vital-signs, labs)
        ("Condition",               0.12),  // ~1,224  (encounter-diagnosis)
        ("Procedure",               0.10),  // ~1,020
        ("MedicationRequest",       0.08),  //   ~816
        ("MedicationAdministration",0.08),  //   ~816
        ("DiagnosticReport",        0.08),  //   ~816
        ("ServiceRequest",          0.07),  //   ~714
        ("Coverage",                0.02),  //   ~204
        ("Specimen",                0.05),  //   ~510
        ("Medication",              0.03),  //   ~306
    ];

    // Varied LOINC codes for Observations to avoid monotonic data
    private static readonly (string Code, string Display, string Category)[] ObservationVariants =
    [
        ("85354-9",  "Blood pressure panel",          "vital-signs"),
        ("8867-4",   "Heart rate",                    "vital-signs"),
        ("8310-5",   "Body temperature",              "vital-signs"),
        ("59408-5",  "Oxygen saturation",             "vital-signs"),
        ("8302-2",   "Body height",                   "vital-signs"),
        ("29463-7",  "Body weight",                   "vital-signs"),
        ("2093-3",   "Total Cholesterol",             "laboratory"),
        ("2571-8",   "Triglycerides",                 "laboratory"),
        ("718-7",    "Hemoglobin",                    "laboratory"),
        ("4544-3",   "Hematocrit",                    "laboratory"),
        ("2160-0",   "Creatinine",                    "laboratory"),
        ("2345-7",   "Glucose",                       "laboratory"),
        ("6690-2",   "WBC",                           "laboratory"),
        ("777-3",    "Platelet count",                "laboratory"),
        ("1742-6",   "ALT",                           "laboratory"),
    ];

    // Varied SNOMED codes for Conditions
    private static readonly (string Code, string Display)[] ConditionVariants =
    [
        ("386661006", "Fever"),
        ("233604007", "Pneumonia"),
        ("84114007",  "Heart failure"),
        ("49436004",  "Atrial fibrillation"),
        ("44054006",  "Type 2 diabetes"),
        ("38341003",  "Hypertensive disorder"),
        ("700097003", "Fracture of hip region"),
        ("195662009", "Acute renal failure"),
    ];

    // Varied SNOMED codes for Procedures
    private static readonly (string Code, string Display)[] ProcedureVariants =
    [
        ("232717009", "Coronary artery bypass grafting"),
        ("18286008",  "Catheterization of urinary bladder"),
        ("225358003", "Wound care"),
        ("40617009",  "Artificial ventilation"),
        ("431231003", "Dialysis procedure"),
        ("447996002", "Insertion of central venous catheter"),
    ];

    // RxNorm codes for medications
    private static readonly (string Code, string Display)[] MedicationVariants =
    [
        ("1049502", "Acetaminophen 325 MG Oral Tablet"),
        ("197696",  "Ceftriaxone 250 MG Injection"),
        ("309362",  "Enoxaparin 40 MG/0.4 ML Injectable"),
        ("835829",  "Vancomycin 500 MG Injection"),
        ("313002",  "Metoprolol 50 MG Oral Tablet"),
        ("197361",  "Furosemide 40 MG Oral Tablet"),
    ];

    public static (List<string> PatientIds, List<(string Name, string Json)> Bundles) Generate(
        ITestOutputHelper output,
        int patientCount = DefaultPatientCount,
        int totalResourcesPerPatient = DefaultResourcesPerPatient,
        string patientIdPrefix = "MegaPatient")
    {
        var patientIds = new List<string>();
        var allEntries = new List<(string PatientId, List<object> Entries)>();

        output.WriteLine($"Generating {patientCount} patients with ~{totalResourcesPerPatient} resources each...");

        // Shared infrastructure resources (created once)
        var sharedEntries = new List<object>
        {
            MakeEntry($"Location/{HospitalLocationId}", MakeLocation(HospitalLocationId, "HOSP", "Main Hospital")),
            MakeEntry($"Location/{IcuLocationId}", MakeLocation(IcuLocationId, "ICU", "Intensive Care Unit")),
            MakeEntry($"Device/{PulseOxDeviceId}", MakeDevice(PulseOxDeviceId, "706689003", "Pulse oximeter")),
            MakeEntry($"Device/{VentilatorDeviceId}", MakeDevice(VentilatorDeviceId, "706172005", "Ventilator")),
        };

        for (var p = 0; p < patientCount; p++)
        {
            var patientId = $"{patientIdPrefix}-{p + 1:D3}";
            patientIds.Add(patientId);

            var encStart = EncounterStart(p);
            var encEnd = EncounterEnd(p);
            var encounterId = $"{patientId}-Encounter-001";

            var entries = new List<object>();

            // Patient
            entries.Add(MakeEntry($"Patient/{patientId}", MakePatient(patientId)));

            // Single inpatient Encounter
            entries.Add(MakeEntry($"Encounter/{encounterId}",
                MakeEncounter(encounterId, patientId, encStart, encEnd)));

            // Track medication IDs for MedicationAdministration references
            var medicationIds = new List<string>();

            // Generate clinical resources tied to this encounter
            var resourceIndex = 0;
            foreach (var (resourceType, fraction) in ResourceDistribution)
            {
                var count = Math.Max(1, (int)(totalResourcesPerPatient * fraction));

                for (var i = 0; i < count; i++)
                {
                    resourceIndex++;
                    var resourceId = $"{patientId}-{resourceType}-{resourceIndex:D5}";
                    // Spread dates across the encounter period
                    var offset = TimeSpan.FromMinutes((double)i / count * (encEnd - encStart).TotalMinutes);
                    var effectiveDate = encStart.Add(offset);

                    var resource = resourceType switch
                    {
                        "Observation" => MakeObservation(resourceId, patientId, encounterId, effectiveDate, i),
                        "Condition" => MakeCondition(resourceId, patientId, encounterId, effectiveDate, i),
                        "Procedure" => MakeProcedure(resourceId, patientId, encounterId, effectiveDate, i),
                        "MedicationRequest" => MakeMedicationRequest(resourceId, patientId, encounterId, effectiveDate, i),
                        "MedicationAdministration" => MakeMedicationAdministration(resourceId, patientId, encounterId, effectiveDate, i, medicationIds),
                        "DiagnosticReport" => MakeDiagnosticReport(resourceId, patientId, encounterId, effectiveDate, i),
                        "ServiceRequest" => MakeServiceRequest(resourceId, patientId, encounterId, effectiveDate, i),
                        "Coverage" => MakeCoverage(resourceId, patientId, encStart, encEnd),
                        "Specimen" => MakeSpecimen(resourceId, patientId, effectiveDate),
                        "Medication" => MakeMedication(resourceId, i, medicationIds),
                        _ => throw new InvalidOperationException($"Unknown resource type: {resourceType}")
                    };

                    entries.Add(MakeEntry($"{resourceType}/{resourceId}", resource));
                }
            }

            output.WriteLine($"  Patient {patientId}: {entries.Count} entries, " +
                             $"encounter={encounterId} ({encStart:yyyy-MM-dd} to {encEnd:yyyy-MM-dd})");

            allEntries.Add((patientId, entries));
        }

        // Build chunked bundles: shared resources first, then patient data
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

        output.WriteLine($"Generated {bundles.Count} bundles for {patientCount} patients.");
        return (patientIds, bundles);
    }

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

    private static object MakePatient(string id) => new Dictionary<string, object>
    {
        ["resourceType"] = "Patient",
        ["id"] = id,
        ["meta"] = new Dictionary<string, object>
        {
            ["profile"] = new[] { "http://hl7.org/fhir/us/core/StructureDefinition/us-core-patient" }
        },
        ["identifier"] = new[]
        {
            new Dictionary<string, object>
            {
                ["system"] = "urn:oid:1.2.840.114350.1.13.93.3.7.3.688884.100",
                ["type"] = new Dictionary<string, object> { ["text"] = "CEID" },
                ["value"] = id
            }
        },
        ["name"] = new[]
        {
            new Dictionary<string, object>
            {
                ["family"] = "MegaTest",
                ["given"] = new[] { $"Patient-{id}" },
                ["use"] = "usual"
            }
        },
        ["gender"] = "male",
        ["birthDate"] = "1965-04-12",
        ["active"] = true,
        ["deceasedBoolean"] = false,
        ["address"] = new[]
        {
            new Dictionary<string, object>
            {
                ["use"] = "billing",
                ["line"] = new[] { "123 Test Street" },
                ["city"] = "TestCity",
                ["state"] = "TX",
                ["postalCode"] = "75001"
            }
        }
    };

    private static object MakeEncounter(string id, string patientId, DateTime start, DateTime end) =>
        new Dictionary<string, object>
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
            ["type"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["coding"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["system"] = "http://snomed.info/sct",
                            ["code"] = "183452005",
                            ["display"] = "Emergency hospital admission (procedure)"
                        }
                    },
                    ["text"] = "Emergency hospital admission (procedure)"
                }
            },
            ["subject"] = new Dictionary<string, object>
            {
                ["reference"] = $"Patient/{patientId}"
            },
            ["period"] = new Dictionary<string, object>
            {
                ["start"] = start.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["end"] = end.ToString("yyyy-MM-ddTHH:mm:ssZ")
            },
            ["location"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["location"] = new Dictionary<string, object> { ["reference"] = $"Location/{HospitalLocationId}" },
                    ["status"] = "completed"
                },
                new Dictionary<string, object>
                {
                    ["location"] = new Dictionary<string, object> { ["reference"] = $"Location/{IcuLocationId}" },
                    ["status"] = "completed"
                }
            },
            ["reasonCode"] = new[]
            {
                SnomedConcept("386661006", "Fever"),
                SnomedConcept("233604007", "Pneumonia")
            },
            ["serviceType"] = new Dictionary<string, object>
            {
                ["coding"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["system"] = "http://terminology.hl7.org/CodeSystem/service-type",
                        ["code"] = "320",
                        ["display"] = "Dieticians"
                    }
                }
            }
        };

    private static object MakeObservation(string id, string patientId, string encounterId, DateTime effective, int index)
    {
        var variant = ObservationVariants[index % ObservationVariants.Length];
        return new Dictionary<string, object>
        {
            ["resourceType"] = "Observation",
            ["id"] = id,
            ["status"] = "final",
            ["category"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["coding"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["system"] = "http://terminology.hl7.org/CodeSystem/observation-category",
                            ["code"] = variant.Category
                        }
                    }
                }
            },
            ["code"] = LoincConcept(variant.Code, variant.Display),
            ["subject"] = Ref($"Patient/{patientId}"),
            ["encounter"] = Ref($"Encounter/{encounterId}"),
            ["effectiveDateTime"] = effective.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["valueQuantity"] = new Dictionary<string, object>
            {
                ["value"] = 70 + (index % 50),
                ["unit"] = variant.Category == "vital-signs" ? "mmHg" : "mg/dL",
                ["system"] = "http://unitsofmeasure.org"
            }
        };
    }

    private static object MakeCondition(string id, string patientId, string encounterId, DateTime onset, int index)
    {
        var variant = ConditionVariants[index % ConditionVariants.Length];
        return new Dictionary<string, object>
        {
            ["resourceType"] = "Condition",
            ["id"] = id,
            ["clinicalStatus"] = CodeableConcept("http://terminology.hl7.org/CodeSystem/condition-clinical", "active"),
            ["verificationStatus"] = CodeableConcept("http://terminology.hl7.org/CodeSystem/condition-ver-status", "confirmed"),
            ["category"] = new[]
            {
                CodeableConcept("http://terminology.hl7.org/CodeSystem/condition-category", "encounter-diagnosis")
            },
            ["code"] = SnomedConcept(variant.Code, variant.Display),
            ["subject"] = Ref($"Patient/{patientId}"),
            ["encounter"] = Ref($"Encounter/{encounterId}"),
            ["onsetDateTime"] = onset.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
    }

    private static object MakeProcedure(string id, string patientId, string encounterId, DateTime performed, int index)
    {
        var variant = ProcedureVariants[index % ProcedureVariants.Length];
        return new Dictionary<string, object>
        {
            ["resourceType"] = "Procedure",
            ["id"] = id,
            ["status"] = "completed",
            ["code"] = SnomedConcept(variant.Code, variant.Display),
            ["subject"] = Ref($"Patient/{patientId}"),
            ["encounter"] = Ref($"Encounter/{encounterId}"),
            ["performedDateTime"] = performed.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
    }

    private static object MakeMedicationRequest(string id, string patientId, string encounterId, DateTime authored, int index)
    {
        var variant = MedicationVariants[index % MedicationVariants.Length];
        return new Dictionary<string, object>
        {
            ["resourceType"] = "MedicationRequest",
            ["id"] = id,
            ["status"] = "active",
            ["intent"] = "order",
            ["medicationCodeableConcept"] = RxNormConcept(variant.Code, variant.Display),
            ["subject"] = Ref($"Patient/{patientId}"),
            ["encounter"] = Ref($"Encounter/{encounterId}"),
            ["authoredOn"] = authored.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
    }

    private static object MakeMedicationAdministration(string id, string patientId, string encounterId,
        DateTime effective, int index, List<string> medicationIds)
    {
        var variant = MedicationVariants[index % MedicationVariants.Length];
        var admin = new Dictionary<string, object>
        {
            ["resourceType"] = "MedicationAdministration",
            ["id"] = id,
            ["status"] = "completed",
            ["subject"] = Ref($"Patient/{patientId}"),
            ["context"] = Ref($"Encounter/{encounterId}"),
            ["effectiveDateTime"] = effective.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        };

        // Reference a Medication resource if one exists, otherwise use inline coding
        if (medicationIds.Count > 0)
        {
            admin["medicationReference"] = Ref($"Medication/{medicationIds[index % medicationIds.Count]}");
        }
        else
        {
            admin["medicationCodeableConcept"] = RxNormConcept(variant.Code, variant.Display);
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
            ["code"] = RxNormConcept(variant.Code, variant.Display)
        };
    }

    private static object MakeDiagnosticReport(string id, string patientId, string encounterId, DateTime effective, int index) =>
        new Dictionary<string, object>
        {
            ["resourceType"] = "DiagnosticReport",
            ["id"] = id,
            ["status"] = "final",
            ["code"] = LoincConcept("58410-2", "CBC panel"),
            ["subject"] = Ref($"Patient/{patientId}"),
            ["encounter"] = Ref($"Encounter/{encounterId}"),
            ["effectiveDateTime"] = effective.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

    private static object MakeServiceRequest(string id, string patientId, string encounterId, DateTime authored, int index) =>
        new Dictionary<string, object>
        {
            ["resourceType"] = "ServiceRequest",
            ["id"] = id,
            ["status"] = "active",
            ["intent"] = "order",
            ["code"] = LoincConcept("24331-1", "Lipid panel"),
            ["subject"] = Ref($"Patient/{patientId}"),
            ["encounter"] = Ref($"Encounter/{encounterId}"),
            ["authoredOn"] = authored.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

    private static object MakeCoverage(string id, string patientId, DateTime start, DateTime end) =>
        new Dictionary<string, object>
        {
            ["resourceType"] = "Coverage",
            ["id"] = id,
            ["status"] = "active",
            ["beneficiary"] = Ref($"Patient/{patientId}"),
            ["payor"] = new[] { Ref($"Patient/{patientId}") },
            ["period"] = new Dictionary<string, object>
            {
                ["start"] = start.ToString("yyyy-MM-dd"),
                ["end"] = end.ToString("yyyy-MM-dd")
            }
        };

    private static object MakeSpecimen(string id, string patientId, DateTime collected) =>
        new Dictionary<string, object>
        {
            ["resourceType"] = "Specimen",
            ["id"] = id,
            ["status"] = "available",
            ["type"] = SnomedConcept("119297000", "Blood specimen"),
            ["subject"] = Ref($"Patient/{patientId}"),
            ["collection"] = new Dictionary<string, object>
            {
                ["collectedDateTime"] = collected.ToString("yyyy-MM-ddTHH:mm:ssZ")
            }
        };

    private static object MakeLocation(string id, string typeCode, string name) =>
        new Dictionary<string, object>
        {
            ["resourceType"] = "Location",
            ["id"] = id,
            ["status"] = "active",
            ["name"] = name,
            ["identifier"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["system"] = "http://example.org/fhir/sid/location",
                    ["value"] = id
                }
            },
            ["type"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["coding"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["system"] = "http://terminology.hl7.org/CodeSystem/v3-RoleCode",
                            ["code"] = typeCode,
                            ["display"] = name
                        }
                    }
                }
            }
        };

    private static object MakeDevice(string id, string snomedCode, string display) =>
        new Dictionary<string, object>
        {
            ["resourceType"] = "Device",
            ["id"] = id,
            ["status"] = "active",
            ["type"] = SnomedConcept(snomedCode, display)
        };

    // ---------------------------------------------------------------
    //  Helpers
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

    private static Dictionary<string, object> Ref(string reference) =>
        new() { ["reference"] = reference };

    private static Dictionary<string, object> LoincConcept(string code, string display) => new()
    {
        ["coding"] = new[]
        {
            new Dictionary<string, object>
            {
                ["system"] = "http://loinc.org",
                ["code"] = code,
                ["display"] = display
            }
        }
    };

    private static Dictionary<string, object> SnomedConcept(string code, string display) => new()
    {
        ["coding"] = new[]
        {
            new Dictionary<string, object>
            {
                ["system"] = "http://snomed.info/sct",
                ["code"] = code,
                ["display"] = display
            }
        }
    };

    private static Dictionary<string, object> RxNormConcept(string code, string display) => new()
    {
        ["coding"] = new[]
        {
            new Dictionary<string, object>
            {
                ["system"] = "http://www.nlm.nih.gov/research/umls/rxnorm",
                ["code"] = code,
                ["display"] = display
            }
        }
    };

    private static Dictionary<string, object> CodeableConcept(string system, string code) => new()
    {
        ["coding"] = new[]
        {
            new Dictionary<string, object>
            {
                ["system"] = system,
                ["code"] = code
            }
        }
    };
}
