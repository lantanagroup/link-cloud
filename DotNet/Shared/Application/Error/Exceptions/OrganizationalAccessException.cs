namespace LantanaGroup.Link.Shared.Application.Error.Exceptions;

/// <summary>
/// Exception thrown when a user or process attempts to access an organization they are not authorized for.
/// </summary>
public class OrganizationalAccessException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrganizationalAccessException"/> class.
    /// </summary>
    public OrganizationalAccessException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrganizationalAccessException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public OrganizationalAccessException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrganizationalAccessException"/> class with organization identifiers.
    /// </summary>
    /// <param name="organizationId">The expected organization identifier.</param>
    /// <param name="requestedOrganizationId">The requested organization identifier.</param>
    public OrganizationalAccessException(string organizationId, string requestedOrganizationId)
        : base($"Organization {requestedOrganizationId} does not match the expected organization {organizationId}.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrganizationalAccessException"/> class with a user and organization identifier.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="organizationId">The organization identifier.</param>
    public OrganizationalAccessException(Guid userId, string organizationId)
        : base($"User {userId} does not have access to organization {organizationId}.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrganizationalAccessException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public OrganizationalAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}