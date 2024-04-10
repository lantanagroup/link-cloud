namespace LantanaGroup.Link.Normalization.Application.Models.Exceptions;

public class BadOperationException : Exception
{
    public BadOperationException(string message) : base(message)
    {
    }

    public BadOperationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public BadOperationException()
    {
    }
}
