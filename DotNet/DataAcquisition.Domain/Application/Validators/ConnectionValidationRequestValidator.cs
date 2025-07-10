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
        if(string.IsNullOrWhiteSpace(facilityId))
        {
            errorMessage = "No Facility ID was provided. One is required to validate.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(patientId) && string.IsNullOrWhiteSpace(patientIdentifier))
        {
            errorMessage = "No Patient ID or Patient Identifier was provided. One is required to validate.";
            return false;
        }

        if(string.IsNullOrWhiteSpace(measureId))
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
}
