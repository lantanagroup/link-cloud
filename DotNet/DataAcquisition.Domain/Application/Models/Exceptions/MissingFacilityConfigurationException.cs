namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;

public class MissingFacilityConfigurationException : Exception
{
    public MissingFacilityConfigurationException(string message) : base(message)
    {
    }

    public MissingFacilityConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public MissingFacilityConfigurationException()
    {
    }
}
