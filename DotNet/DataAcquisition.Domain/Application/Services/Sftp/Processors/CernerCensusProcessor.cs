using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Processors;

/// <summary>
/// Processor for Cerner SFTP census acquisition.
/// Handles <see cref="SftpAcquisitionType.CernerCensus"/> and produces CernerPatientsAcquired Kafka events.
/// Produces one event per file containing patient encounter data.
/// </summary>
public class CernerCensusProcessor(
    ILogger<CernerCensusProcessor> logger,
    ISftpClientService sftpClientService,
    IFileParserFactory fileParserFactory,
    IProducer<string, CernerPatientsAcquired> kafkaProducer)
    : ISftpAcquisitionProcessor
{
    /// <inheritdoc/>
    public bool CanProcess(SftpAcquisitionType acquisitionType)
    {
        return acquisitionType == SftpAcquisitionType.CernerCensus;
    }

    /// <inheritdoc/>
    public async Task<List<string>> ProcessAsync(
        SftpAcquisitionLog log,
        SftpConfigurationModel sftpConfig,
        SftpAcquisitionTypeConfiguration acquisitionConfig,
        CancellationToken cancellationToken)
    {
        // Open session and delegate to the session-aware method
        await using var session = await sftpClientService.OpenSessionAsync(sftpConfig, cancellationToken);
        return await ProcessWithSessionAsync(log, session, sftpConfig, acquisitionConfig, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<string>> ProcessWithSessionAsync(
        SftpAcquisitionLog log,
        ISftpSession session,
        SftpConfigurationModel sftpConfig,
        SftpAcquisitionTypeConfiguration acquisitionConfig,
        CancellationToken cancellationToken)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity();

        var remoteDir = acquisitionConfig.RemoteDirectory ?? sftpConfig.RemoteDirectory ?? "/";
        var processedFiles = new List<string>();
        var totalEncounterCount = 0;

        // List matching files
        var files = await session.ListFilesAsync(
            remoteDir,
            acquisitionConfig.FileNamePattern,
            cancellationToken);

        // Check if any files were found
        if (files.Count == 0)
        {
            activity?.AddEvent(new ActivityEvent(
                "No Files Found",
                tags: new ActivityTagsCollection
                {
                    { "FacilityId", log.FacilityId },
                    { "AcquisitionType", log.AcquisitionType.ToString() },
                    { "Pattern", acquisitionConfig.FileNamePattern ?? "(all files)" }
                }));

            logger.LogInformation(
                "No files found for facility {FacilityId}, type {AcquisitionType}, pattern {Pattern}",
                log.FacilityId, log.AcquisitionType, acquisitionConfig.FileNamePattern ?? "(all files)");
            return processedFiles;
        }
        
        activity?.AddEvent(new ActivityEvent(
            "Files Found",
            tags: new ActivityTagsCollection
            {
                { "FacilityId", log.FacilityId },
                { "AcquisitionType", log.AcquisitionType.ToString() },
                { "Pattern", acquisitionConfig.FileNamePattern ?? "(all files)" },
                { "FileCount", files.Count }
            }));

        logger.LogInformation(
            "Found {FileCount} files for facility {FacilityId}, type {AcquisitionType}",
            files.Count, log.FacilityId, log.AcquisitionType);

        // Process each file
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogDebug("Processing file {FileName} for facility {FacilityId}", file.Name, log.FacilityId);

            // Get appropriate parser for this file
            var fileExtension = Path.GetExtension(file.Name);
            var parser = fileParserFactory.GetParser<CernerEncounters>(
                log.AcquisitionType,
                fileExtension,
                acquisitionConfig.ParsingConfiguration);

            // Download and parse file
            using var fileStream = await session.DownloadFileAsync(file.FullName, cancellationToken);

            //TODO: We should consider saving the files to process locally rather than keeping the connection open during parsing.
            
            var fileEncounters = new List<CernerEncounters>();
            await foreach (var encounter in parser.ParseAsync(
                fileStream, acquisitionConfig.ParsingConfiguration, cancellationToken))
            {
                fileEncounters.Add(encounter);
            }

            // Produce Kafka event for patients found in this file
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

            await kafkaProducer.ProduceAsync(
                nameof(KafkaTopic.CernerPatientsAcquired), kafkaMessage, cancellationToken);

            logger.LogDebug(
                "Produced CernerPatientsAcquired event for file {FileName} with {EncounterCount} encounters",
                file.Name, fileEncounters.Count);

            // Handle post-processing: move to processed directory or delete
            if (!string.IsNullOrWhiteSpace(acquisitionConfig.ProcessedDirectory))
            {
                // Move file to processed directory
                await session.MoveFileAsync(file.FullName, acquisitionConfig.ProcessedDirectory, cancellationToken);
                logger.LogDebug("Moved file {FileName} to {ProcessedDirectory}", file.Name, acquisitionConfig.ProcessedDirectory);
            }
            else if (sftpConfig.RemoveAfterProcessing)
            {
                // Delete file
                await session.DeleteFileAsync(file.FullName, cancellationToken);
                logger.LogDebug("Deleted file {FileName} after processing", file.Name);
            }
            else
            {
                // File left in place - will be re-processed on next run
                logger.LogWarning(
                    "File {FileName} left in place. Configure ProcessedDirectory or enable RemoveAfterProcessing to avoid re-processing.",
                    file.Name);
            }

            processedFiles.Add(file.Name);
            totalEncounterCount += fileEncounters.Count;
        }

        // Flush all produced messages
        kafkaProducer.Flush(cancellationToken);

        logger.LogInformation(
            "Successfully processed {FileCount} Cerner census files with {EncounterCount} total encounters for facility {FacilityId}",
            processedFiles.Count, totalEncounterCount, log.FacilityId);

        return processedFiles;
    }
}
