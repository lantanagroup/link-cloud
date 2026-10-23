using System.Globalization;
using Hl7.Fhir.Model;
using LantanaGroup.Link.PatientFlowSimulator.Planning;
using LantanaGroup.Link.PatientFlowSimulator.Scenario;
using List = Hl7.Fhir.Model.List;

namespace LantanaGroup.Link.PatientFlowSimulator.Generation;

/// <summary>
/// Builds the FHIR resources of a stay. Ids are derived from the patient id, so regenerating a month
/// produces the same resources and every upload is an idempotent PUT.
/// </summary>
public static class FhirResourceFactory
{
    public const string ActCodeSystem = "http://terminology.hl7.org/CodeSystem/v3-ActCode";
    public const string HslocSystem = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
    public const string RxNormSystem = "http://www.nlm.nih.gov/research/umls/rxnorm";
    public const string LoincSystem = "http://loinc.org";
    public const string UcumSystem = "http://unitsofmeasure.org";

    private const string IdentifierSystemPrefix = "urn:link:patient-flow-simulator";
    private const string ObservationCategorySystem = "http://terminology.hl7.org/CodeSystem/observation-category";
    private const string ConditionCategorySystem = "http://terminology.hl7.org/CodeSystem/condition-category";
    private const string ConditionClinicalSystem = "http://terminology.hl7.org/CodeSystem/condition-clinical";
    private const string ConditionVerificationSystem = "http://terminology.hl7.org/CodeSystem/condition-ver-status";
    private const string MedicationRequestCategorySystem = "http://terminology.hl7.org/CodeSystem/medicationrequest-category";
    private const string MedicationAdminCategorySystem = "http://terminology.hl7.org/CodeSystem/medication-admin-category";
    private const string IdentifierTypeSystem = "http://terminology.hl7.org/CodeSystem/v2-0203";

    /// <summary>
    /// Id of the facility's Organization.
    /// </summary>
    public static string OrganizationId(FacilityDefinition facility) => $"{facility.FacilityId}-org";

    /// <summary>
    /// Id of a facility unit. One Location per distinct HSLOC code.
    /// </summary>
    public static string LocationId(FacilityDefinition facility, LocationSpec location)
        => $"{facility.FacilityId}-loc-{location.Hsloc}";

    /// <summary>
    /// The facility as an Organization.
    /// </summary>
    public static Organization BuildOrganization(FacilityDefinition facility) => new()
    {
        Id = OrganizationId(facility),
        Active = true,
        Name = facility.Name,
        Identifier = [new Identifier($"{IdentifierSystemPrefix}:facility", facility.FacilityId)]
    };

    /// <summary>
    /// A unit of the facility, typed with its HSLOC code.
    /// </summary>
    public static Location BuildLocation(FacilityDefinition facility, LocationSpec location) => new()
    {
        Id = LocationId(facility, location),
        Status = Location.LocationStatus.Active,
        Name = location.Name,
        Type = [new CodeableConcept(HslocSystem, location.Hsloc, location.HslocDisplay)],
        ManagingOrganization = OrganizationReference(facility)
    };

    /// <summary>
    /// The patient of a stay.
    /// </summary>
    public static Patient BuildPatient(PatientStay stay) => new()
    {
        Id = stay.PatientId,
        Active = true,
        Identifier =
        [
            new Identifier($"{IdentifierSystemPrefix}:{stay.Facility.FacilityId}:mrn", stay.PatientId)
            {
                Type = new CodeableConcept(IdentifierTypeSystem, "MR", "Medical record number")
            }
        ],
        Name =
        [
            new HumanName
            {
                Use = HumanName.NameUse.Official,
                Family = stay.Identity.FamilyName,
                Given = [stay.Identity.GivenName]
            }
        ],
        Gender = stay.Identity.Gender == "female" ? AdministrativeGender.Female : AdministrativeGender.Male,
        BirthDate = stay.Identity.BirthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ManagingOrganization = OrganizationReference(stay.Facility)
    };

    /// <summary>
    /// The stay's encounter, in progress at admission and finished at discharge.
    /// </summary>
    public static Encounter BuildEncounter(PatientStay stay, bool finished)
    {
        var template = stay.Template;
        var encounter = new Encounter
        {
            Id = stay.EncounterId,
            Identifier = [new Identifier($"{IdentifierSystemPrefix}:{stay.Facility.FacilityId}:visit", $"{stay.PatientId}-V1")],
            Status = finished ? Encounter.EncounterStatus.Finished : Encounter.EncounterStatus.InProgress,
            Class = new Coding(ActCodeSystem, template.EncounterClass, EncounterClassDisplay(template.EncounterClass)),
            Subject = PatientReference(stay),
            Period = BuildPeriod(stay, finished),
            ServiceProvider = OrganizationReference(stay.Facility)
        };

        if (template.EncounterType != null)
        {
            encounter.Type = [ToCodeableConcept(template.EncounterType)];
        }

        if (template.Location != null)
        {
            encounter.Location =
            [
                new Encounter.LocationComponent
                {
                    Location = new ResourceReference($"Location/{LocationId(stay.Facility, template.Location)}", template.Location.Name),
                    Status = finished ? Encounter.EncounterLocationStatus.Completed : Encounter.EncounterLocationStatus.Active,
                    Period = BuildPeriod(stay, finished)
                }
            ];
        }

        if (finished)
        {
            encounter.Hospitalization = new Encounter.HospitalizationComponent
            {
                DischargeDisposition = ToCodeableConcept(template.DischargeDisposition)
            };
        }

        return encounter;
    }

    /// <summary>
    /// The stay's encounter diagnoses, picked from the template's pool.
    /// </summary>
    public static IReadOnlyList<Condition> BuildConditions(PatientStay stay)
    {
        var random = DeterministicRandom.Create(stay.Seed, "conditions");
        var pool = stay.Template.Conditions.ToList();
        var result = new List<Condition>();

        for (var n = 1; n <= stay.Template.ConditionsPerStay && pool.Count > 0; n++)
        {
            var spec = pool[random.Next(pool.Count)];
            pool.Remove(spec);
            result.Add(new Condition
            {
                Id = $"{stay.PatientId}-cond-{n}",
                ClinicalStatus = new CodeableConcept(ConditionClinicalSystem, "active", "Active"),
                VerificationStatus = new CodeableConcept(ConditionVerificationSystem, "confirmed", "Confirmed"),
                Category = [new CodeableConcept(ConditionCategorySystem, "encounter-diagnosis", "Encounter Diagnosis")],
                Code = ToCodeableConcept(spec),
                Subject = PatientReference(stay),
                Encounter = EncounterReference(stay),
                Onset = new FhirDateTime(stay.AdmitUtc),
                RecordedDateElement = new FhirDateTime(stay.AdmitUtc.AddMinutes(30))
            });
        }

        return result;
    }

    /// <summary>
    /// One order per template medication, written at admission.
    /// </summary>
    public static IReadOnlyList<MedicationRequest> BuildMedicationRequests(PatientStay stay)
    {
        var result = new List<MedicationRequest>();

        foreach (var spec in stay.Template.Medications)
        {
            result.Add(new MedicationRequest
            {
                Id = MedicationRequestId(stay, spec),
                Status = MedicationRequest.MedicationrequestStatus.Active,
                Intent = MedicationRequest.MedicationRequestIntent.Order,
                Category = [new CodeableConcept(MedicationRequestCategorySystem, "inpatient", "Inpatient")],
                Medication = new CodeableConcept(RxNormSystem, spec.RxNorm, spec.Display),
                Subject = PatientReference(stay),
                Encounter = EncounterReference(stay),
                AuthoredOnElement = new FhirDateTime(stay.AdmitUtc.AddHours(1)),
                DosageInstruction =
                [
                    new Dosage
                    {
                        Text = DosageText(spec),
                        Route = ToCodeableConcept(spec.Route)
                    }
                ]
            });
        }

        return result;
    }

    /// <summary>
    /// One completed administration of a template medication.
    /// </summary>
    public static MedicationAdministration BuildMedicationAdministration(StayEvent stayEvent)
    {
        var stay = stayEvent.Stay;
        var spec = stayEvent.Medication
                   ?? throw new ArgumentException("A medication administration event needs a medication.", nameof(stayEvent));

        return new MedicationAdministration
        {
            Id = $"{stay.PatientId}-medadm-{spec.RxNorm}-{stayEvent.Sequence}",
            Status = MedicationAdministration.MedicationAdministrationStatusCodes.Completed,
            Category = new CodeableConcept(MedicationAdminCategorySystem, "inpatient", "Inpatient"),
            Medication = new CodeableConcept(RxNormSystem, spec.RxNorm, spec.Display),
            Subject = PatientReference(stay),
            Context = EncounterReference(stay),
            Effective = new FhirDateTime(stayEvent.WhenUtc),
            Request = new ResourceReference($"MedicationRequest/{MedicationRequestId(stay, spec)}"),
            Dosage = new MedicationAdministration.DosageComponent
            {
                Text = DosageText(spec),
                Route = ToCodeableConcept(spec.Route),
                Dose = new Quantity
                {
                    Value = (decimal)spec.DoseValue,
                    Unit = spec.DoseUnit,
                    System = UcumSystem,
                    Code = spec.DoseUnit
                }
            }
        };
    }

    /// <summary>
    /// One final observation of a template observation.
    /// </summary>
    public static Observation BuildObservation(StayEvent stayEvent)
    {
        var stay = stayEvent.Stay;
        var spec = stayEvent.Observation
                   ?? throw new ArgumentException("An observation event needs an observation.", nameof(stayEvent));
        var categoryDisplay = spec.Category == "laboratory" ? "Laboratory" : "Vital Signs";

        return new Observation
        {
            Id = $"{stay.PatientId}-obs-{spec.Loinc}-{stayEvent.Sequence}",
            Status = ObservationStatus.Final,
            Category = [new CodeableConcept(ObservationCategorySystem, spec.Category, categoryDisplay)],
            Code = new CodeableConcept(LoincSystem, spec.Loinc, spec.Display),
            Subject = PatientReference(stay),
            Encounter = EncounterReference(stay),
            Effective = new FhirDateTime(stayEvent.WhenUtc),
            Issued = stayEvent.WhenUtc.AddMinutes(5),
            Value = new Quantity
            {
                Value = (decimal)stayEvent.Value,
                Unit = spec.Unit,
                System = UcumSystem,
                Code = spec.Unit
            }
        };
    }

    /// <summary>
    /// A census list as Link's DataAcquisition reads it: one Patient reference per entry.
    /// </summary>
    public static List BuildCensusList(CensusListSnapshot snapshot, FacilityDefinition facility, DateTimeOffset asOfUtc) => new()
    {
        Id = snapshot.ListId,
        Status = List.ListStatus.Current,
        Mode = ListMode.Working,
        Title = $"{facility.Name} - {snapshot.Title}",
        Code = new CodeableConcept { Text = "Census patient list" },
        DateElement = new FhirDateTime(asOfUtc),
        Entry = snapshot.Entries
            .Select(e => new List.EntryComponent
            {
                Item = new ResourceReference($"Patient/{e.PatientId}", e.DisplayName)
            })
            .ToList()
    };

    /// <summary>
    /// Wraps a resource as a PUT entry, so uploads are idempotent.
    /// </summary>
    public static Bundle.EntryComponent PutEntry(Resource resource) => new()
    {
        Resource = resource,
        Request = new Bundle.RequestComponent
        {
            Method = Bundle.HTTPVerb.PUT,
            Url = $"{resource.TypeName}/{resource.Id}"
        }
    };

    /// <summary>
    /// A transaction bundle of the given entries.
    /// </summary>
    public static Bundle TransactionBundle(IEnumerable<Bundle.EntryComponent> entries, DateTimeOffset timestamp) => new()
    {
        Type = Bundle.BundleType.Transaction,
        Timestamp = timestamp,
        Entry = entries.ToList()
    };

    private static string MedicationRequestId(PatientStay stay, MedicationSpec spec)
        => $"{stay.PatientId}-medreq-{spec.RxNorm}";

    private static string DosageText(MedicationSpec spec)
        => string.Create(CultureInfo.InvariantCulture,
                         $"{spec.DoseValue} {spec.DoseUnit} {spec.Route.Display} every {spec.EveryHours} hours");

    private static Period BuildPeriod(PatientStay stay, bool finished)
    {
        var period = new Period { StartElement = new FhirDateTime(stay.AdmitUtc) };
        if (finished)
        {
            period.EndElement = new FhirDateTime(stay.DischargeUtc);
        }

        return period;
    }

    private static ResourceReference PatientReference(PatientStay stay)
        => new($"Patient/{stay.PatientId}", stay.Identity.DisplayName);

    private static ResourceReference EncounterReference(PatientStay stay)
        => new($"Encounter/{stay.EncounterId}");

    private static ResourceReference OrganizationReference(FacilityDefinition facility)
        => new($"Organization/{OrganizationId(facility)}", facility.Name);

    private static CodeableConcept ToCodeableConcept(CodeSpec spec)
        => new(spec.System, spec.Code, spec.Display);

    private static string EncounterClassDisplay(string code) => code.ToUpperInvariant() switch
    {
        "IMP" => "inpatient encounter",
        "AMB" => "ambulatory",
        "EMER" => "emergency",
        "OBSENC" => "observation encounter",
        "NONAC" => "inpatient non-acute",
        "ACUTE" => "inpatient acute",
        "SS" => "short stay",
        _ => code
    };
}
