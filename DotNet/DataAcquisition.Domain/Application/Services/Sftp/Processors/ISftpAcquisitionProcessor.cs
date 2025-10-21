using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Processors;

/// <summary>
/// Defines operations for processing SFTP acquisition requests.
/// Each implementation handles a specific <see cref="SftpAcquisitionType"/> and produces
/// its own Kafka events based on the data retrieved from the SFTP server.
/// </summary>
public interface ISftpAcquisitionProcessor
{
    /// <summary>
    /// Determines if this processor can handle the given acquisition type and subtype.
    /// </summary>
    /// <param name="acquisitionType">The type of SFTP acquisition to check.</param>
    /// <param name="subType">The subtype of SFTP acquisition to check.</param>
    /// <returns><c>true</c> if this processor can handle the acquisition type and subtype; otherwise, <c>false</c>.</returns>
    bool CanProcess(SftpAcquisitionType acquisitionType, SftpAcquisitionSubType subType);

    /// <summary>
    /// Processes an SFTP acquisition log entry, managing its own SFTP session internally.
    /// Use this method for single-log processing scenarios where session management is not a concern.
    /// The processor will open a new SFTP connection, download and parse files, produce Kafka events,
    /// and close the connection when complete.
    /// </summary>
    /// <param name="log">The acquisition log entry containing facility and processing metadata.</param>
    /// <param name="sftpConfig">The SFTP server configuration including host, port, and connection settings.</param>
    /// <param name="acquisitionConfig">The acquisition-specific configuration including remote directory and file patterns.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <param name="benchmark">Optional benchmark collector for performance metrics.</param>
    /// <returns>A list of file names that were successfully processed.</returns>
    Task<List<string>> ProcessAsync(
        SftpAcquisitionLog log,
        SftpConfigurationModel sftpConfig,
        SftpAcquisitionTypeConfiguration acquisitionConfig,
        CancellationToken cancellationToken,
        SftpBenchmarkCollector? benchmark = null);

    /// <summary>
    /// Processes an SFTP acquisition log entry using a provided SFTP session.
    /// Use this method for batch processing scenarios where multiple logs for the same facility
    /// should share a single SFTP connection to avoid connection overhead.
    /// The caller is responsible for opening the session before calling and disposing it after all logs are processed.
    /// </summary>
    /// <param name="log">The acquisition log entry containing facility and processing metadata.</param>
    /// <param name="session">An open SFTP session to use for file operations.</param>
    /// <param name="sftpConfig">The SFTP server configuration including host, port, and connection settings.</param>
    /// <param name="acquisitionConfig">The acquisition-specific configuration including remote directory and file patterns.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <param name="benchmark">Optional benchmark collector for performance metrics.</param>
    /// <returns>A list of file names that were successfully processed.</returns>
    Task<List<string>> ProcessWithSessionAsync(
        SftpAcquisitionLog log,
        ISftpSession session,
        SftpConfigurationModel sftpConfig,
        SftpAcquisitionTypeConfiguration acquisitionConfig,
        CancellationToken cancellationToken,
        SftpBenchmarkCollector? benchmark = null);
}
