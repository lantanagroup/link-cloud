namespace LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;

public class MissingFacilityConfigurationException : Exception
{
    public MissingFacilityConfigurationException()
    {
    }

    public MissingFacilityConfigurationException(string message) : base(message)
    {
    }

    public MissingFacilityConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}