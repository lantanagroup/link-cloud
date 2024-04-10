using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.Exceptions;

public class DeserializationUnsupportedTypeException : Exception
{
    public DeserializationUnsupportedTypeException()
    {
    }

    public DeserializationUnsupportedTypeException(string? message) : base(message)
    {
    }

    public DeserializationUnsupportedTypeException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    protected DeserializationUnsupportedTypeException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
