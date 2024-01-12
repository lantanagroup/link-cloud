using System.Runtime.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Models.Exceptions;
[Serializable]
internal class TenantNotFoundException : Exception
{
    public TenantNotFoundException()
    {
    }

    public TenantNotFoundException(string? message) : base(message)
    {
    }

    public TenantNotFoundException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    protected TenantNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}