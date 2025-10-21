using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class DocumentReferenceFactory
{
    /// <summary>Generate a DocumentReference from the document type pool using a seed index.</summary>
    public static DocumentReference Generate(
        string id, string patientId, string encounterId,
        DateTime created, int seed, string orgId, string authorPractId)
    {
        var d = FhirGenerationCodes.DocumentTypes[seed % FhirGenerationCodes.DocumentTypes.Length];
        return Create(id, patientId, encounterId, created, orgId, authorPractId,
                      d.Code, d.Display, d.ClassCode, d.ClassDisplay);
    }

    /// <summary>Create a DocumentReference with caller-supplied document type values.</summary>
    public static DocumentReference Create(
        string id,
        string patientId,
        string encounterId,
        DateTime created,
        string orgId,
        string authorPractId,
        string loincCode,
        string display,
        string classCode,
        string classDisplay)
    {
        var contentText = BuildNoteText(patientId, display, created);
        var contentBytes = System.Text.Encoding.UTF8.GetBytes(contentText);

        return new DocumentReference
        {
            Id = id,
            MasterIdentifier = new Identifier
            {
                System = "urn:ietf:rfc:3986",
                Value = $"urn:uuid:{id}"
            },
            Status = DocumentReferenceStatus.Current,
            DocStatus = CompositionStatus.Final,
            Type = Loinc(loincCode, display),
            Category =
            [
                new CodeableConcept
                {
                    Coding = [new Coding("http://loinc.org", classCode, classDisplay)],
                    Text   = classDisplay
                }
            ],
            Subject = Ref($"Patient/{patientId}"),
            DateElement = new Instant(new DateTimeOffset(created)),
            Author = [Ref($"Practitioner/{authorPractId}", "Authoring Clinician")],
            Authenticator = Ref($"Organization/{orgId}", "General Test Hospital"),
            Custodian = Ref($"Organization/{orgId}", "General Test Hospital"),
            Description = $"{display} - {created:yyyy-MM-dd}",
            SecurityLabel =
            [
                CC("http://terminology.hl7.org/CodeSystem/v3-Confidentiality", "N", "Normal")
            ],
            Content =
            [
                new DocumentReference.ContentComponent
                {
                    Attachment = new Attachment
                    {
                        ContentType   = "text/plain; charset=utf-8",
                        Language      = "en-US",
                        DataElement   = new Base64Binary(contentBytes),
                        Title         = $"{display} for Patient {patientId}",
                        Creation      = created.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    },
                    Format = new Coding(
                        "http://ihe.net/fhir/ihe.formatcode.fhir/CodeSystem/formatcode",
                        "urn:ihe:iti:xds:2017:mimeTypeSufficient",
                        "mimeType Sufficient")
                }
            ],
            Context = new DocumentReference.ContextComponent
            {
                Encounter = [Ref($"Encounter/{encounterId}")],
                Period = new Period
                {
                    StartElement = new FhirDateTime(created),
                    EndElement = new FhirDateTime(created.AddHours(1))
                },
                FacilityType = CC("http://terminology.hl7.org/CodeSystem/v3-RoleCode", "HOSP", "Hospital"),
                PracticeSetting = CC("http://snomed.info/sct", "394802001", "General medicine")
            }
        };
    }

    private static string BuildNoteText(string patientId, string noteType, DateTime date) => noteType switch
    {
        "Discharge summary" => $"DISCHARGE SUMMARY\nPatient: {patientId}\nDate: {date:yyyy-MM-dd}\nDiagnosis: See problem list.\nCondition at discharge: Stable.\nFollow-up: Outpatient clinic in 2 weeks.",
        "History and physical note" => $"HISTORY & PHYSICAL\nPatient: {patientId}\nDate: {date:yyyy-MM-dd}\nCC: Presenting complaint documented.\nPMH: Hypertension, Diabetes.\nExam: Within normal limits except as noted.",
        "Progress note" => $"PROGRESS NOTE\nPatient: {patientId}\nDate: {date:yyyy-MM-dd}\nS: Patient reports improvement.\nO: Vitals stable. Labs reviewed.\nA/P: Continue current management.",
        _ => $"{noteType.ToUpper()}\nPatient: {patientId}\nDate: {date:yyyy-MM-dd}\nClinical documentation for inpatient encounter."
    };
}
