namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;

public class FhirConnectionFailedException : Exception
{
    public FhirConnectionFailedException() : base("Failed to connect to FHIR API.")
    {
    }

    public FhirConnectionFailedException(string message) : base(message)
    {
    }

    public FhirConnectionFailedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
