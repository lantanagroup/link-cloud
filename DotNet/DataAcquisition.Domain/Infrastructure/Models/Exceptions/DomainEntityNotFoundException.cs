namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Exceptions;
public class DomainEntityNotFoundException : Exception
{
    public DomainEntityNotFoundException(string message) : base(message)
    {
    }

    public DomainEntityNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
