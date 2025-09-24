namespace DataAcquisition.Domain.Application.Models.Exceptions;
public class MaxRetriesExceededException : Exception
{
    public MaxRetriesExceededException()
    {
    }
    public MaxRetriesExceededException(string message)
        : base(message)
    {
    }
    public MaxRetriesExceededException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
