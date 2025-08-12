namespace DataAcquisition.Domain.Application.Models.Exceptions;
public class IncorrectQueryPlanOrderException : Exception
{
    public IncorrectQueryPlanOrderException(string message) : base(message)
    {
    }
    public IncorrectQueryPlanOrderException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
