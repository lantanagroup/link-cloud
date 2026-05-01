using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

public class SftpConfigurationModel
{
    public Guid Id { get; set; }
    public string OrganizationId { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? RemoteDirectory { get; set; }
    public TimeSpan Timeout { get; set; }
    public bool RemoveAfterProcessing { get; set; }
    public AuthType AuthenticationProtocol { get; set; }

    /// <summary>
    /// Enables detailed benchmark timing collection for this facility.
    /// Useful during onboarding to measure SFTP performance.
    /// </summary>
    public bool EnableBenchmarking { get; set; }

    /// <summary>
    /// List of acquisition configurations for different data types.
    /// Each configuration specifies its acquisition type, directory, file pattern, and parsing rules.
    /// </summary>
    public List<SftpAcquisitionTypeConfiguration> AcquisitionConfigurations { get; set; } = [];
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
            OrganizationId = entity.OrganizationId,
            Host = entity.Host,
            Port = entity.Port,
            RemoteDirectory = entity.RemoteDirectory,
            Timeout = entity.Timeout,
            RemoveAfterProcessing = entity.RemoveAfterProcessing,
            AuthenticationProtocol = entity.AuthenticationProtocol,
            EnableBenchmarking = entity.EnableBenchmarking,
            AcquisitionConfigurations = entity.AcquisitionConfigurations
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
            AuthenticationProtocol = model.AuthenticationProtocol,
            EnableBenchmarking = model.EnableBenchmarking,
            AcquisitionConfigurations = model.AcquisitionConfigurations
        };
    }
}