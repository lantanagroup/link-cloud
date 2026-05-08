namespace LantanaGroup.Link.Normalization.Application.Models.Exceptions;

public class MissingCorrelationIdException : Exception
{
    public MissingCorrelationIdException() : base("CorrelationId is missing.")
    {
    }

    public MissingCorrelationIdException(string message) : base(message)
    {
    }

    public MissingCorrelationIdException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
