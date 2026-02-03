using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;

/// <summary>
/// Base interface for SFTP acquisition processors.
/// Each implementation handles a specific acquisition type and produces its own Kafka events.
/// </summary>
public interface ISftpAcquisitionProcessor
{
    /// <summary>
    /// Determines if this processor can handle the given acquisition type.
    /// </summary>
    bool CanProcess(SftpAcquisitionType acquisitionType);

    /// <summary>
    /// Processes an SFTP acquisition log entry.
    /// Downloads files, parses data, and produces Kafka events.
    /// Returns the list of processed file names.
    /// </summary>
    Task<List<string>> ProcessAsync(
        SftpAcquisitionLog log,
        SftpConfigurationModel sftpConfig,
        SftpAcquisitionTypeConfiguration acquisitionConfig,
        CancellationToken cancellationToken);
}
