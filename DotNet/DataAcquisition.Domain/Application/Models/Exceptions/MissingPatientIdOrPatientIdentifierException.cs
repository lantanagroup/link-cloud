namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;

public class MissingPatientIdOrPatientIdentifierException : Exception
{
    public MissingPatientIdOrPatientIdentifierException() : base("No Patient ID or Patient Identifier was provided. One is required to validate.")
    {
    }

    public MissingPatientIdOrPatientIdentifierException(string message) : base(message)
    {
    }

    public MissingPatientIdOrPatientIdentifierException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
