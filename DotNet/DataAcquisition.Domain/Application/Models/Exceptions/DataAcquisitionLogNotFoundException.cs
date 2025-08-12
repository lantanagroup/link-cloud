namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
[Serializable]
public class DataAcquisitionLogNotFoundException : Exception
{
    public DataAcquisitionLogNotFoundException()
    {
    }

    public DataAcquisitionLogNotFoundException(string? message) : base(message)
    {
    }

    public DataAcquisitionLogNotFoundException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}