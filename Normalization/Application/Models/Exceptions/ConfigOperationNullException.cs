namespace LantanaGroup.Link.Normalization.Application.Models.Exceptions;

public class ConfigOperationNullException : Exception
{
    public ConfigOperationNullException()
    {
    }

    public ConfigOperationNullException(string? message) : base(message)
    {
    }
}
