namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;

// One of the facility's six patient lists could not be read from the EHR - most often because the
// FHIR id entered for that list in the Census step doesn't exist on the FHIR server. Unlike
// LinkServiceException's deliberately generic 502, Detail carries Data Acquisition's own message,
// which names the failing FhirId, so the facility can tell which of the six fields to fix.
public class PatientListRetrievalFailedException : Exception
{
    public PatientListRetrievalFailedException(string facilityId, string detail)
        : base(detail)
    {
        FacilityId = facilityId;
    }

    public string FacilityId { get; }
}
