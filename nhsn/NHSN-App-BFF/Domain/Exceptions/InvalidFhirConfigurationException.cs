namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;

// Unlike LinkServiceException's deliberately generic 502, this is about facility-entered data, not
// a Link outage - safe to report to the facility. ErrorCode is what the frontend actually shows: a
// stable, translatable key (see onboarding:errors.* in Localization/en-US/onboarding.json). Detail
// stays in English as a fallback for any consumer that doesn't recognize the code (logs, Swagger).
public class InvalidFhirConfigurationException : Exception
{
    public InvalidFhirConfigurationException(string facilityId, string errorCode, string detail)
        : base(detail)
    {
        FacilityId = facilityId;
        ErrorCode = errorCode;
    }

    public string FacilityId { get; }
    public string ErrorCode { get; }
}
