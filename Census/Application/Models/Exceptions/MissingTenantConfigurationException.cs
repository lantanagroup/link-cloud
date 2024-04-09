namespace LantanaGroup.Link.Census.Application.Models.Exceptions;

public class MissingTenantConfigurationException : Exception
{
    public MissingTenantConfigurationException(string message) : base(message)
    {
    }

    public MissingTenantConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public MissingTenantConfigurationException()
    {
    }
}