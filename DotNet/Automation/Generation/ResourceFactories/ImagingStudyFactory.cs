using Hl7.Fhir.Model;
using static LantanaGroup.Automation.Generation.ResourceFactories.FhirConceptFactory;

namespace LantanaGroup.Automation.Generation.ResourceFactories;

public static class ImagingStudyFactory
{
    /// <summary>Generate an ImagingStudy from the pool using a seed index.</summary>
    public static ImagingStudy Generate(
        string id, string patientId, string encounterId,
        DateTime started, int seed, string locationId, string interpreterPractitionerId)
    {
        var v = FhirGenerationCodes.ImagingStudies[seed % FhirGenerationCodes.ImagingStudies.Length];
        return Create(id, patientId, encounterId, started, locationId, interpreterPractitionerId,
                      v.SnomedCode, v.Display, v.Modality,
                      v.BodySiteCode, v.BodySiteDisplay,
                      v.ReasonCode, v.ReasonDisplay);
    }

    /// <summary>Create an ImagingStudy with caller-supplied values.</summary>
    public static ImagingStudy Create(
        string id,
        string patientId,
        string encounterId,
        DateTime started,
        string locationId,
        string interpreterPractitionerId,
        string snomedCode,
        string display,
        string modality,
        string bodySiteCode,
        string bodySiteDisplay,
        string reasonCode,
        string reasonDisplay)
    {
        var stableHash = id.GetStableHash32();
        var studyUid = $"1.2.840.99999.{stableHash}.1";
        var seriesUid = $"{studyUid}.1";
        var instanceUid = $"{seriesUid}.1";

        return new ImagingStudy
        {
            Id = id,
            Identifier =
            [
                new Identifier
                {
                    Use    = Identifier.IdentifierUse.Official,
                    System = "urn:dicom:uid",
                    Value  = $"urn:oid:{studyUid}"
                }
            ],
            Status = ImagingStudy.ImagingStudyStatus.Available,
            Modality = [new Coding("http://dicom.nema.org/resources/ontology/DCM", modality, modality)],
            Subject = Ref($"Patient/{patientId}"),
            Encounter = Ref($"Encounter/{encounterId}"),
            StartedElement = new FhirDateTime(started),
            NumberOfSeries = 1,
            NumberOfInstances = 1,
            ProcedureCode = [Snomed(snomedCode, display)],
            ReasonCode = [Snomed(reasonCode, reasonDisplay)],
            Location = Ref($"Location/{locationId}"),
            Interpreter = [Ref($"Practitioner/{interpreterPractitionerId}", "Radiologist")],
            Note = [new Annotation { Text = new Markdown($"Study performed for: {reasonDisplay}. Interpreted by radiology.") }],
            Series =
            [
                new ImagingStudy.SeriesComponent
                {
                    Uid               = seriesUid,
                    Number            = 1,
                    Modality          = new Coding("http://dicom.nema.org/resources/ontology/DCM", modality, modality),
                    Description       = $"{display} series 1",
                    NumberOfInstances = 1,
                    BodySite          = new Coding("http://snomed.info/sct", bodySiteCode, bodySiteDisplay),
                    StartedElement    = new FhirDateTime(started),
                    Performer         = [],
                    Instance =
                    [
                        new ImagingStudy.InstanceComponent
                        {
                            Uid      = instanceUid,
                            SopClass = new Coding("urn:ietf:rfc:3986", "urn:oid:1.2.840.10008.5.1.4.1.1.3.1"),
                            Number   = 1,
                            Title    = $"{display} - Image 1"
                        }
                    ]
                }
            ]
        };
    }
}
