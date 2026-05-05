using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Parsers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Processors;

/// <summary>
/// Processor for Cerner CCL Extract SFTP census acquisition.
/// Handles <see cref="SftpAcquisitionType.Census"/> with <see cref="SftpAcquisitionSubType.CernerCCLExtract"/>
/// and produces CernerPatientsAcquired Kafka events.
/// Produces one event per file containing patient encounter data.
/// </summary>
public class CernerCclExtractProcessor(
    ILogger<CernerCclExtractProcessor> logger,
    ISftpClientService sftpClientService,
    IFileParserFactory fileParserFactory,
    IProducer<string, CernerPatientsAcquired> kafkaProducer)
    : ISftpAcquisitionProcessor
{
    /// <inheritdoc/>
    public bool CanProcess(SftpAcquisitionType acquisitionType, SftpAcquisitionSubType subType)
    {
        return acquisitionType == SftpAcquisitionType.Census
               && subType == SftpAcquisitionSubType.CernerCCLExtract;
    }

    /// <inheritdoc/>
    public async Task<List<string>> ProcessAsync(
        SftpAcquisitionLog log,
        SftpConfigurationModel sftpConfig,
        SftpAcquisitionTypeConfiguration acquisitionConfig,
        CancellationToken cancellationToken,
        SftpBenchmarkCollector? benchmark = null)
    {
        // Open session and delegate to the session-aware method
        await using var session = await sftpClientService.OpenSessionAsync(sftpConfig, cancellationToken);
        return await ProcessWithSessionAsync(log, session, sftpConfig, acquisitionConfig, cancellationToken, benchmark);
    }

    /// <inheritdoc/>
    public async Task<List<string>> ProcessWithSessionAsync(
        SftpAcquisitionLog log,
        ISftpSession session,
        SftpConfigurationModel sftpConfig,
        SftpAcquisitionTypeConfiguration acquisitionConfig,
        CancellationToken cancellationToken,
        SftpBenchmarkCollector? benchmark = null)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity();

        var remoteDir = acquisitionConfig.RemoteDirectory ?? sftpConfig.RemoteDirectory ?? "/";
        var processedFiles = new List<string>();
        var totalEncounterCount = 0;

        // List matching files
        benchmark?.StartConnectionAndRetrieval();
        var files = await session.ListFilesAsync(
            remoteDir,
            acquisitionConfig.FileNamePattern,
            cancellationToken);
        benchmark?.EndConnectionAndRetrieval();

        // Check if any files were found
        if (files.Count == 0)
        {
            activity?.AddEvent(new ActivityEvent(
                "No Files Found",
                tags: new ActivityTagsCollection
                {
                    { DiagnosticNames.FacilityId, log.FacilityId },
                    { "acquisition.type", log.AcquisitionType.ToString() },
                    { "sub.type", acquisitionConfig.SubType.ToString() },
                    { "file.pattern", acquisitionConfig.FileNamePattern ?? "(all files)" }
                }));

            logger.LogInformation(
                "No files found for facility {FacilityId}, type {AcquisitionType}, sub-type {SubType}, pattern {Pattern}",
                log.FacilityId.SanitizeForLog(), log.AcquisitionType.SanitizeForLog(), log.SubType.SanitizeForLog(), acquisitionConfig.FileNamePattern.SanitizeForLog() ?? "(all files)");
            return processedFiles;
        }

        // Add files found activity event
        activity?.AddEvent(new ActivityEvent(
            "Files Found",
            tags: new ActivityTagsCollection
            {
                { DiagnosticNames.FacilityId, log.FacilityId },
                { "acquisition.type", log.AcquisitionType.ToString() },
                { "file.pattern", acquisitionConfig.FileNamePattern ?? "(all files)" },
                { "file.count", files.Count },
                { "file.names", string.Join(", ", files.Select(f => f.Name)) }
            }));

        logger.LogInformation(
            "Found {FileCount} {FileLabel} for facility {FacilityId}, type {AcquisitionType}, sub-type {SubType}, pattern {Pattern}",
            files.Count, files.Count == 1 ? "file" : "files", log.FacilityId.SanitizeForLog(), log.AcquisitionType.SanitizeForLog(), log.SubType.SanitizeForLog(), acquisitionConfig.FileNamePattern.SanitizeForLog() ?? "(all files)");

        // Process each file
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogDebug("Processing file {FileName} for facility {FacilityId}", file.Name.SanitizeForLog(), log.FacilityId.SanitizeForLog());

            // Get appropriate parser for this file
            var fileExtension = Path.GetExtension(file.Name);
            var parser = fileParserFactory.GetParser<CernerEncounters>(
                log.AcquisitionType,
                acquisitionConfig.SubType,
                fileExtension,
                acquisitionConfig.ParsingConfiguration);

            // Download file
            benchmark?.StartConnectionAndRetrieval();
            using var fileStream = await session.DownloadFileAsync(file.FullName, cancellationToken);
            benchmark?.EndConnectionAndRetrieval();

            //TODO: We should consider saving the files to process locally rather than keeping the connection open during parsing.

            // Parse file
            benchmark?.StartParse();
            var fileEncounters = new List<CernerEncounters>();
            await foreach (var encounter in parser.ParseAsync(
                fileStream, acquisitionConfig.ParsingConfiguration, cancellationToken))
            {
                fileEncounters.Add(encounter);
            }
            benchmark?.EndParse(fileEncounters.Count);

            // Produce Kafka event for patients found in this file
            var kafkaMessage = new Message<string, CernerPatientsAcquired>
            {
                Key = log.FacilityId,
                Headers = [new Header("X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()))],
                Value = new CernerPatientsAcquired
                {
                    PatientEncounters = fileEncounters
                }
            };

            await kafkaProducer.ProduceAsync(
                nameof(KafkaTopic.CernerPatientsAcquired), kafkaMessage, cancellationToken);

            // Add encounters identified activity event
            activity?.AddEvent(new ActivityEvent(
                "Encounters Identified",
                tags: new ActivityTagsCollection
                {
                    { "file.name", file.Name },
                    { "encounter.count", fileEncounters.Count },
                    { "encounter.identifiers", string.Join(", ", fileEncounters.Select(f => f.EncounterId)) }
                }));

            logger.LogDebug(
                "Produced CernerPatientsAcquired event for file {FileName} with {EncounterCount} encounters",
                file.Name, fileEncounters.Count);

            // Handle post-processing: move to processed directory or delete
            if (!string.IsNullOrWhiteSpace(acquisitionConfig.ProcessedDirectory))
            {
                // Move file to processed directory
                await session.MoveFileAsync(file.FullName, acquisitionConfig.ProcessedDirectory, cancellationToken);
                logger.LogDebug("Moved file {FileName} to {ProcessedDirectory}", file.Name.SanitizeForLog(), acquisitionConfig.ProcessedDirectory.SanitizeForLog());
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
            "Successfully processed {FileCount} {FileLabel} with {EncounterCount} {EncounterLabel} for facility {FacilityId}",
            processedFiles.Count.SanitizeForLog(), processedFiles.Count == 1 ? "file" : "files",
            totalEncounterCount.SanitizeForLog(), totalEncounterCount == 1 ? "encounter" : "encounters",
            log.FacilityId.SanitizeForLog());

        return processedFiles;
    }
}
