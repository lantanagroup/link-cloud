using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class EncounterFactory
{
    /// <summary>Generate an inpatient Encounter driven by the ClinicalScenario for this seed.</summary>
    public static Encounter Generate(
        string id,
        string patientId,
        DateTime start,
        DateTime end,
        string attendingPractId,
        string admittingPractId,
        string edLocationId,
        string icuLocationId,
        string stepDownLocationId,
        string orgId,
        string primaryDiagnosisConditionId,
        int seed)
    {
        var scenario = FhirGenerationCodes.ClinicalScenarios[seed % FhirGenerationCodes.ClinicalScenarios.Length];

        return Create(
            id, patientId, start, end,
            attendingPractId, admittingPractId,
            edLocationId, icuLocationId, stepDownLocationId, orgId,
            primaryDiagnosisConditionId,
            scenario.AdmitTypeCode, scenario.AdmitTypeDisplay,
            scenario.PrimaryDxSnomed, scenario.PrimaryDxDisplay, scenario.PrimaryDxIcd,
            scenario.AdmitSourceCode, scenario.AdmitSourceDisplay,
            scenario.DischargeDispositionCode, scenario.DischargeDispositionDisplay,
            scenario.ServiceTypeCode, scenario.ServiceTypeDisplay,
            scenario.PriorityCode, scenario.PriorityDisplay);
    }

    /// <summary>Create an Encounter with fully caller-supplied values.</summary>
    public static Encounter Create(
        string id,
        string patientId,
        DateTime start,
        DateTime end,
        string attendingPractId,
        string admittingPractId,
        string edLocationId,
        string icuLocationId,
        string stepDownLocationId,
        string orgId,
        string primaryDiagnosisConditionId,
        string encTypeCode,
        string encTypeDisplay,
        string reasonSnomedCode,
        string reasonDisplay,
        string reasonIcdCode,
        string admitSourceCode,
        string admitSourceDisplay,
        string dischargeDispositionCode,
        string dischargeDispositionDisplay,
        string serviceTypeCode,
        string serviceTypeDisplay,
        string priorityCode,
        string priorityDisplay) => new()
        {
            Id = id,
            Status = Encounter.EncounterStatus.Finished,
            Identifier =
        [
            new Identifier
            {
                Use = Identifier.IdentifierUse.Official,
                System = "http://example.org/fhir/sid/encounter",
                Value = id
            }
        ],
            Class = new Coding("http://terminology.hl7.org/CodeSystem/v3-ActCode", "IMP", "inpatient encounter"),
            Type =
        [
            new CodeableConcept
            {
                Coding = [new Coding("http://snomed.info/sct", encTypeCode, encTypeDisplay)],
                Text   = encTypeDisplay
            }
        ],
            ServiceType = new CodeableConcept
            {
                Coding = [new Coding("http://terminology.hl7.org/CodeSystem/service-type", serviceTypeCode, serviceTypeDisplay)],
                Text = serviceTypeDisplay
            },
            Priority = CC("http://terminology.hl7.org/CodeSystem/v3-ActPriority", priorityCode, priorityDisplay),
            Subject = Ref($"Patient/{patientId}", $"Patient {patientId}"),
            Participant =
        [
            new Encounter.ParticipantComponent
            {
                Type =
                [
                    new CodeableConcept
                    {
                        Coding = [new Coding("http://terminology.hl7.org/CodeSystem/v3-ParticipationType", "ATND", "attender")],
                        Text   = "Attending"
                    }
                ],
                Period     = new Period { StartElement = new FhirDateTime(start), EndElement = new FhirDateTime(end) },
                Individual = Ref($"Practitioner/{attendingPractId}", "Attending Physician")
            },
            new Encounter.ParticipantComponent
            {
                Type =
                [
                    new CodeableConcept
                    {
                        Coding = [new Coding("http://terminology.hl7.org/CodeSystem/v3-ParticipationType", "ADM", "admitter")],
                        Text   = "Admitting"
                    }
                ],
                Period     = new Period { StartElement = new FhirDateTime(start), EndElement = new FhirDateTime(start.AddHours(2)) },
                Individual = Ref($"Practitioner/{admittingPractId}", "Admitting Physician")
            }
        ],
            Period = new Period { StartElement = new FhirDateTime(start), EndElement = new FhirDateTime(end) },
            Length = new Duration { Value = (decimal)(end - start).TotalDays, Unit = "days", System = "http://unitsofmeasure.org", Code = "d" },
            // Reason code carries the ICD-10 for claim-level coding
            ReasonCode =
        [
            new CodeableConcept
            {
                Coding =
                [
                    new Coding("http://snomed.info/sct",            reasonSnomedCode, reasonDisplay),
                    new Coding("http://hl7.org/fhir/sid/icd-10-cm", reasonIcdCode,    reasonDisplay)
                ],
                Text = reasonDisplay
            }
        ],
            // diagnosis.use = AD (Admission Diagnosis) with rank 1, linking to the Condition resource
            Diagnosis =
        [
            new Encounter.DiagnosisComponent
            {
                Condition = Ref($"Condition/{primaryDiagnosisConditionId}"),
                Use       = CC("http://terminology.hl7.org/CodeSystem/diagnosis-role", "AD", "Admission diagnosis"),
                Rank      = 1
            }
        ],
            Hospitalization = new Encounter.HospitalizationComponent
            {
                AdmitSource = CC("http://terminology.hl7.org/CodeSystem/admit-source", admitSourceCode, admitSourceDisplay),
                DischargeDisposition = CC("http://terminology.hl7.org/CodeSystem/discharge-disposition", dischargeDispositionCode, dischargeDispositionDisplay)
            },
            Location =
        [
            new Encounter.LocationComponent
            {
                Location = Ref($"Location/{edLocationId}", "Emergency Department"),
                Status   = Encounter.EncounterLocationStatus.Completed,
                Period   = new Period { StartElement = new FhirDateTime(start), EndElement = new FhirDateTime(start.AddHours(4)) }
            },
            new Encounter.LocationComponent
            {
                Location = Ref($"Location/{icuLocationId}", "Intensive Care Unit"),
                Status   = Encounter.EncounterLocationStatus.Completed,
                Period   = new Period { StartElement = new FhirDateTime(start.AddHours(4)), EndElement = new FhirDateTime(end.AddDays(-1)) }
            },
            new Encounter.LocationComponent
            {
                Location = Ref($"Location/{stepDownLocationId}", "Step-Down Unit"),
                Status   = Encounter.EncounterLocationStatus.Completed,
                Period   = new Period { StartElement = new FhirDateTime(end.AddDays(-1)), EndElement = new FhirDateTime(end) }
            }
        ],
            ServiceProvider = Ref($"Organization/{orgId}", "General Test Hospital")
        };

    /// <summary>
    /// Create an ambulatory (outpatient) Encounter that will NOT qualify for
    /// inpatient/ED/observation-based measure Initial Populations.
    /// Uses class=AMB, a single outpatient clinic location, and no hospitalization component.
    /// </summary>
    public static Encounter CreateAmbulatory(
        string id,
        string patientId,
        DateTime start,
        DateTime end,
        string attendingPractId,
        string outpatientLocationId,
        string orgId,
        string primaryDiagnosisConditionId,
        string reasonSnomedCode,
        string reasonDisplay,
        string reasonIcdCode) => new()
        {
            Id = id,
            Status = Encounter.EncounterStatus.Finished,
            Identifier =
        [
            new Identifier
            {
                Use = Identifier.IdentifierUse.Official,
                System = "http://example.org/fhir/sid/encounter",
                Value = id
            }
        ],
            Class = new Coding("http://terminology.hl7.org/CodeSystem/v3-ActCode", "AMB", "ambulatory"),
            Type =
        [
            new CodeableConcept
            {
                Coding = [new Coding("http://snomed.info/sct", "11429006", "Consultation")],
                Text   = "Outpatient consultation"
            }
        ],
            ServiceType = new CodeableConcept
            {
                Coding = [new Coding("http://terminology.hl7.org/CodeSystem/service-type", "305", "General Medicine")],
                Text = "General Medicine"
            },
            Priority = CC("http://terminology.hl7.org/CodeSystem/v3-ActPriority", "R", "routine"),
            Subject = Ref($"Patient/{patientId}", $"Patient {patientId}"),
            Participant =
        [
            new Encounter.ParticipantComponent
            {
                Type =
                [
                    new CodeableConcept
                    {
                        Coding = [new Coding("http://terminology.hl7.org/CodeSystem/v3-ParticipationType", "ATND", "attender")],
                        Text   = "Attending"
                    }
                ],
                Period     = new Period { StartElement = new FhirDateTime(start), EndElement = new FhirDateTime(end) },
                Individual = Ref($"Practitioner/{attendingPractId}", "Attending Physician")
            }
        ],
            Period = new Period { StartElement = new FhirDateTime(start), EndElement = new FhirDateTime(end) },
            Length = new Duration { Value = (decimal)(end - start).TotalHours, Unit = "hours", System = "http://unitsofmeasure.org", Code = "h" },
            ReasonCode =
        [
            new CodeableConcept
            {
                Coding =
                [
                    new Coding("http://snomed.info/sct",            reasonSnomedCode, reasonDisplay),
                    new Coding("http://hl7.org/fhir/sid/icd-10-cm", reasonIcdCode,    reasonDisplay)
                ],
                Text = reasonDisplay
            }
        ],
            Diagnosis =
        [
            new Encounter.DiagnosisComponent
            {
                Condition = Ref($"Condition/{primaryDiagnosisConditionId}"),
                Use       = CC("http://terminology.hl7.org/CodeSystem/diagnosis-role", "AD", "Admission diagnosis"),
                Rank      = 1
            }
        ],
            Location =
        [
            new Encounter.LocationComponent
            {
                Location = Ref($"Location/{outpatientLocationId}", "Outpatient Clinic"),
                Status   = Encounter.EncounterLocationStatus.Completed,
                Period   = new Period { StartElement = new FhirDateTime(start), EndElement = new FhirDateTime(end) }
            }
        ],
            ServiceProvider = Ref($"Organization/{orgId}", "General Test Hospital")
        };
}
