namespace LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;

public class FhirApiFetchFailureException : Exception
{
    public FhirApiFetchFailureException()
    {
    }

    public FhirApiFetchFailureException(string message) : base(message)
    {
    }

    public FhirApiFetchFailureException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
