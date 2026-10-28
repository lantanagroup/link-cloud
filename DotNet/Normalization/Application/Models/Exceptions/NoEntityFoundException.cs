namespace LantanaGroup.Link.Normalization.Application.Models.Exceptions;

public class NoEntityFoundException : Exception
{
    public NoEntityFoundException() : base()
    {
    }

    public NoEntityFoundException(string message) : base(message)
    {
    }
}


