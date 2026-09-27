namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;

public class ConnectionValidationRequestValidator
{
    public static bool ValidateRequest(
        string facilityId,
        string? patientId,
        string? patientIdentifier,
        string? measureId,
        DateTime? start,
        DateTime? end,
        out string errorMessage
        )
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            errorMessage = "No Facility ID was provided. One is required to validate.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(patientId) && string.IsNullOrWhiteSpace(patientIdentifier))
        {
            errorMessage = "No Patient ID or Patient Identifier was provided. One is required to validate.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(measureId))
        {
            errorMessage = "No Measure ID was provided. One is required to validate.";
            return false;
        }

        if (start == default)
        {
            errorMessage = "start date is invalid.";
            return false;
        }

        if (end == default)
        {
            errorMessage = "end date is invalid.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Validates the fhirServerUrl supplied to the configuration-free connection validation endpoint.
    /// </summary>
    public static bool ValidateFhirServerUrl(string? fhirServerUrl, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(fhirServerUrl))
        {
            errorMessage = "No FHIR server URL was provided. One is required to validate.";
            return false;
        }

        if (!Uri.TryCreate(fhirServerUrl.Trim(), UriKind.Absolute, out var uri))
        {
            errorMessage = "The FHIR server URL provided is not a valid absolute URL.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            errorMessage = "The FHIR server URL provided must use the http or https scheme.";
            return false;
        }

        // A base URL is all that is usable here: callers append /metadata to it, so a query string
        // or fragment would end up buried mid-URL and silently address the wrong resource.
        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            errorMessage = "The FHIR server URL provided must be a base URL without a query string or fragment.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
