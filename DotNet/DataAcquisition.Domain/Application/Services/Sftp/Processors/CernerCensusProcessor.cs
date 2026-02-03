using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Processors;

/// <summary>
/// Processor for Cerner SFTP census acquisition.
/// Handles SftpAcquisitionType.CernerCensus and produces CernerPatientsAcquired Kafka events.
/// Produces one event per file.
/// </summary>
public class CernerCensusProcessor : ISftpAcquisitionProcessor
{
    private readonly ILogger<CernerCensusProcessor> _logger;
    private readonly ISftpClientService _sftpClientService;
    private readonly IFileParserFactory _fileParserFactory;
    private readonly IProducer<string, CernerPatientsAcquired> _kafkaProducer;

    public CernerCensusProcessor(
        ILogger<CernerCensusProcessor> logger,
        ISftpClientService sftpClientService,
        IFileParserFactory fileParserFactory,
        IProducer<string, CernerPatientsAcquired> kafkaProducer)
    {
        _logger = logger;
        _sftpClientService = sftpClientService;
        _fileParserFactory = fileParserFactory;
        _kafkaProducer = kafkaProducer;
    }

    public bool CanProcess(SftpAcquisitionType acquisitionType)
    {
        return acquisitionType == SftpAcquisitionType.CernerCensus;
    }

    public async Task<List<string>> ProcessAsync(
        SftpAcquisitionLog log,
        SftpConfiguration sftpConfig,
        SftpAcquisitionTypeConfiguration acquisitionConfig,
        CancellationToken cancellationToken)
    {
        var remoteDir = acquisitionConfig.RemoteDirectory ?? sftpConfig.RemoteDirectory ?? "/";
        var processedFiles = new List<string>();
        var totalEncounterCount = 0;

        // Open a single SFTP session for all operations
        await using var session = await _sftpClientService.OpenSessionAsync(sftpConfig, cancellationToken);

        // List matching files
        var files = await session.ListFilesAsync(
            remoteDir,
            acquisitionConfig.FileNamePattern,
            cancellationToken);

        if (files.Count == 0)
        {
            _logger.LogInformation(
                "No files found for facility {FacilityId}, type {AcquisitionType}, pattern {Pattern}",
                log.FacilityId, log.AcquisitionType, acquisitionConfig.FileNamePattern ?? "(all files)");
            return processedFiles;
        }

        _logger.LogInformation(
            "Found {FileCount} files for facility {FacilityId}, type {AcquisitionType}",
            files.Count, log.FacilityId, log.AcquisitionType);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogDebug("Processing file {FileName} for facility {FacilityId}", file.Name, log.FacilityId);

            // Get appropriate parser for this file
            var fileExtension = Path.GetExtension(file.Name);
            var parser = _fileParserFactory.GetParser<CernerEncounters>(
                log.AcquisitionType,
                fileExtension,
                acquisitionConfig.ParsingConfiguration);

            // Download and parse file (same session/connection)
            using var fileStream = await session.DownloadFileAsync(file.FullName, cancellationToken);

            var fileEncounters = new List<CernerEncounters>();
            await foreach (var encounter in parser.ParseAsync(
                fileStream, acquisitionConfig.ParsingConfiguration, cancellationToken))
            {
                fileEncounters.Add(encounter);
            }

            // Produce Kafka event for THIS file
            var kafkaMessage = new Message<string, CernerPatientsAcquired>
            {
                Key = log.FacilityId,
                Headers = new Headers
                {
                    new Header("X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()))
                },
                Value = new CernerPatientsAcquired
                {
                    PatientEncounters = fileEncounters
                }
            };

            await _kafkaProducer.ProduceAsync(
                KafkaTopic.CernerPatientsAcquired.ToString(), kafkaMessage, cancellationToken);

            _logger.LogDebug(
                "Produced CernerPatientsAcquired event for file {FileName} with {EncounterCount} encounters",
                file.Name, fileEncounters.Count);

            // Handle post-processing: move to processed directory or delete
            if (!string.IsNullOrWhiteSpace(acquisitionConfig.ProcessedDirectory))
            {
                // Move file to processed directory (preferred - allows audit/recovery)
                await session.MoveFileAsync(file.FullName, acquisitionConfig.ProcessedDirectory, cancellationToken);
                _logger.LogDebug("Moved file {FileName} to {ProcessedDirectory}", file.Name, acquisitionConfig.ProcessedDirectory);
            }
            else if (sftpConfig.RemoveAfterProcessing)
            {
                // Delete file (legacy behavior)
                await session.DeleteFileAsync(file.FullName, cancellationToken);
                _logger.LogDebug("Deleted file {FileName} after processing", file.Name);
            }
            else
            {
                // File left in place - will be re-processed on next run
                _logger.LogWarning(
                    "File {FileName} left in place. Configure ProcessedDirectory or enable RemoveAfterProcessing to avoid re-processing.",
                    file.Name);
            }

            processedFiles.Add(file.Name);
            totalEncounterCount += fileEncounters.Count;
        }

        // Flush all produced messages
        _kafkaProducer.Flush(cancellationToken);

        // Session auto-disconnects when disposed

        _logger.LogInformation(
            "Successfully processed {FileCount} Cerner census files with {EncounterCount} total encounters for facility {FacilityId}",
            processedFiles.Count, totalEncounterCount, log.FacilityId);

        return processedFiles;
    }
}
