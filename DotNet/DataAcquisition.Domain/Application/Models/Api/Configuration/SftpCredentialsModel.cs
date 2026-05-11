using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

/// <summary>
/// Model for SFTP basic authentication credentials.
/// Used for create/update operations only - never returned from GET endpoints.
/// </summary>
[DataContract]
public class SftpCredentialsModel
{
    /// <summary>
    /// The username for SFTP authentication.
    /// </summary>
    [DataMember]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The password for SFTP authentication.
    /// </summary>
    [DataMember]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Response model indicating credential status without exposing actual credential values.
/// </summary>
public class SftpCredentialStatusModel
{
    /// <summary>
    /// Indicates whether credentials have been configured for the organization.
    /// </summary>
    public bool HasCredentials { get; set; }

    /// <summary>
    /// The timestamp when credentials were last updated, if available.
    /// </summary>
    public DateTime? LastUpdated { get; set; }
}
