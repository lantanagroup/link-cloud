using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

public class SftpConfigurationModel
{
    public Guid Id { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? RemoteDirectory { get; set; }
    public TimeSpan Timeout { get; set; }
    public bool RemoveAfterProcessing { get; set; }
    public AuthType AuthenticationProtocol { get; set; }
}

/// <summary>
/// Model for creating SFTP configuration with optional credentials.
/// Credentials are stored separately in Azure Key Vault, not in the database.
/// </summary>
public class CreateSftpConfigurationModel : SftpConfigurationModel
{
    /// <summary>
    /// Optional credentials for basic authentication.
    /// Will be stored in Azure Key Vault, not in the database.
    /// </summary>
    public SftpCredentialsModel? Credentials { get; set; }
}

public static class SftpConfigurationModelExtensions
{
    public static SftpConfigurationModel ToModel(this SftpConfiguration entity)
    {
        return new SftpConfigurationModel
        {
            Id = entity.Id,
            Host = entity.Host,
            Port = entity.Port,
            RemoteDirectory = entity.RemoteDirectory,
            Timeout = entity.Timeout,
            RemoveAfterProcessing = entity.RemoveAfterProcessing,
            AuthenticationProtocol = entity.AuthenticationProtocol
        };
    }
    
    public static SftpConfiguration ToEntity(this SftpConfigurationModel model, string organizationId)
    {
        return new SftpConfiguration
        {
            Id = model.Id,
            OrganizationId = organizationId,
            Host = model.Host,
            Port = model.Port,
            RemoteDirectory = model.RemoteDirectory,
            Timeout = model.Timeout,
            RemoveAfterProcessing = model.RemoveAfterProcessing,
            AuthenticationProtocol = model.AuthenticationProtocol
        };
    }
}