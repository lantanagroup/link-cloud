using System.Runtime.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Exceptions;
public class EntityPotentiallyExistsException : Exception
{
    public EntityPotentiallyExistsException()
    {
    }

    public EntityPotentiallyExistsException(string? message) : base(message)
    {
    }

    public EntityPotentiallyExistsException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    [Obsolete("Formatter-based serialization is obsolete", DiagnosticId = "SYSLIB0051")]
    protected EntityPotentiallyExistsException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }


}
