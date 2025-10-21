namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
public class PatientListRetrievalFailedException : Exception
{
    public PatientListRetrievalFailedException(string message) : base(message)
    {
    }
    public PatientListRetrievalFailedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
