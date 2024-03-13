namespace LantanaGroup.Link.Census.Models.Exceptions;

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