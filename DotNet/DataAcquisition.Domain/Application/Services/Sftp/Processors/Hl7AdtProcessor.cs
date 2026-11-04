using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Hl7;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Parsers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Processors;

/// <summary>
/// Processor for HL7 v2 ADT census feeds over SFTP. Handles <see cref="SftpAcquisitionType.Census"/> with
/// <see cref="SftpAcquisitionSubType.HL7ADT"/> and produces ADTAcquired events: one per file, split in order when a
/// file is too large for one Kafka message.
/// </summary>
/// <remarks>
/// An event that cannot be tied to a patient is logged and left out, but failing to reach the FHIR server throws, so
/// the acquisition is retried with the file still in place. Logs identify messages by file, position and control id,
/// never by MRN.
/// </remarks>
public partial class Hl7AdtProcessor(
    ILogger<Hl7AdtProcessor> logger,
    ISftpClientService sftpClientService,
    IFileParserFactory fileParserFactory,
    IFhirQueryConfigurationQueries fhirQueryConfigurationQueries,
    IAdtPatientIdResolver patientIdResolver,
    IProducer<string, ADTAcquired> kafkaProducer,
    IOptions<SftpAcquisitionSettings> settings)
    : ISftpAcquisitionProcessor
{
    // Valid FHIR id: catches a configured location that points at something else, such as a whole CX field
    [GeneratedRegex(@"^[A-Za-z0-9\-.]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex FhirIdRegex();

    /// <inheritdoc/>
    public bool CanProcess(SftpAcquisitionType acquisitionType, SftpAcquisitionSubType subType)
    {
        return acquisitionType == SftpAcquisitionType.Census && subType == SftpAcquisitionSubType.HL7ADT;
    }

    /// <inheritdoc/>
    public async Task<List<string>> ProcessAsync(
        SftpAcquisitionLog log,
        SftpConfigurationModel sftpConfig,
        SftpAcquisitionTypeConfiguration acquisitionConfig,
        CancellationToken cancellationToken,
        SftpBenchmarkCollector? benchmark = null)
    {
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
        activity?.SetTag(DiagnosticNames.FacilityId, log.FacilityId);

        // InvalidOperationException marks the log as needing configuration rather than retrying it
        var adtConfig = acquisitionConfig.AdtConfiguration
            ?? throw new InvalidOperationException($"Facility {log.FacilityId} has no ADT configuration for its HL7 ADT SFTP acquisition.");

        var context = await BuildContextAsync(log.FacilityId, adtConfig, cancellationToken);

        var remoteDir = acquisitionConfig.RemoteDirectory ?? sftpConfig.RemoteDirectory ?? "/";
        var processedFiles = new List<string>();
        var totalEventCount = 0;

        benchmark?.StartConnectionAndRetrieval();
        var listedFiles = await session.ListFilesAsync(remoteDir, acquisitionConfig.FileNamePattern, cancellationToken);
        benchmark?.EndConnectionAndRetrieval();

        if (listedFiles.Count == 0)
        {
            logger.LogInformation(
                "No ADT files found for facility {FacilityId}, pattern {Pattern}",
                log.FacilityId.SanitizeForLog(), acquisitionConfig.FileNamePattern.SanitizeForLog() ?? "(all files)");
            return processedFiles;
        }

        // ADT events are a stream, so files must be read oldest first. SFTP timestamps are coarse: break ties by name.
        var files = listedFiles
            .OrderBy(f => f.LastWriteTime)
            .ThenBy(f => f.Name, StringComparer.Ordinal)
            .ToList();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parser = fileParserFactory.GetParser<Hl7Message>(
                log.AcquisitionType,
                acquisitionConfig.SubType,
                Path.GetExtension(file.Name),
                acquisitionConfig.ParsingConfiguration);

            benchmark?.StartConnectionAndRetrieval();
            using var fileStream = await session.DownloadFileAsync(file.FullName, cancellationToken);
            benchmark?.EndConnectionAndRetrieval();

            benchmark?.StartParse();
            var entries = new List<AdtEventEntry>();
            var messageCount = 0;
            await foreach (var message in parser.ParseAsync(fileStream, acquisitionConfig.ParsingConfiguration, cancellationToken))
            {
                messageCount++;
                var entry = await MapMessageAsync(message, file.Name, log.FacilityId, context, cancellationToken);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }
            benchmark?.EndParse(entries.Count);

            if (messageCount == 0)
            {
                logger.LogError(
                    "File {FileName} for facility {FacilityId} contains no HL7 messages",
                    file.Name.SanitizeForLog(), log.FacilityId.SanitizeForLog());
            }

            foreach (var batch in SplitIntoMessages(entries, settings.Value.AdtMaxMessageBytes))
            {
                await kafkaProducer.ProduceAsync(
                    nameof(KafkaTopic.ADTAcquired),
                    new Message<string, ADTAcquired>
                    {
                        Key = log.FacilityId,
                        Headers = [new Header("X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()))],
                        Value = new ADTAcquired { Events = batch }
                    },
                    cancellationToken);
            }

            activity?.AddEvent(new ActivityEvent(
                "ADT File Processed",
                tags: new ActivityTagsCollection
                {
                    { "file.name", file.Name },
                    { "message.count", messageCount },
                    { "event.count", entries.Count }
                }));

            logger.LogDebug(
                "Read {EventCount} of {MessageCount} ADT messages from file {FileName}",
                entries.Count, messageCount, file.Name.SanitizeForLog());

            if (!string.IsNullOrWhiteSpace(acquisitionConfig.ProcessedDirectory))
            {
                await session.MoveFileAsync(file.FullName, acquisitionConfig.ProcessedDirectory, cancellationToken);
            }
            else if (sftpConfig.RemoveAfterProcessing)
            {
                await session.DeleteFileAsync(file.FullName, cancellationToken);
            }
            else
            {
                // Validation requires one of the above, so this is a configuration saved another way. Census ignores
                // the repeated events, but the file will be read again on every run.
                logger.LogWarning(
                    "ADT file {FileName} left in place. Configure ProcessedDirectory or enable RemoveAfterProcessing to avoid re-processing.",
                    file.Name.SanitizeForLog());
            }

            processedFiles.Add(file.Name);
            totalEventCount += entries.Count;
        }

        kafkaProducer.Flush(cancellationToken);

        logger.LogInformation(
            "Processed {FileCount} ADT files with {EventCount} events for facility {FacilityId}",
            processedFiles.Count, totalEventCount, log.FacilityId.SanitizeForLog());

        return processedFiles;
    }

    private async Task<AdtProcessingContext> BuildContextAsync(string facilityId, AdtConfiguration adtConfig, CancellationToken cancellationToken)
    {
        var usesMrnSearch = !string.IsNullOrWhiteSpace(adtConfig.MrnIdentifierSystem);
        FhirQueryConfigurationModel? queryConfiguration = null;

        if (usesMrnSearch)
        {
            queryConfiguration = await fhirQueryConfigurationQueries.GetByFacilityIdAsync(facilityId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Facility {facilityId} is configured for ADT Patient MRN search but has no FHIR query configuration.");
        }

        return new AdtProcessingContext(
            usesMrnSearch ? null : Hl7Path.Parse(adtConfig.PatientIdLocation ?? string.Empty),
            adtConfig.MrnIdentifierSystem,
            queryConfiguration,
            Hl7Path.Parse(string.IsNullOrWhiteSpace(adtConfig.MrnLocation) ? AdtConfiguration.DefaultMrnLocation : adtConfig.MrnLocation),
            Hl7Path.Parse(string.IsNullOrWhiteSpace(adtConfig.EncounterIdLocation) ? AdtConfiguration.DefaultEncounterIdLocation : adtConfig.EncounterIdLocation));
    }

    private async Task<AdtEventEntry?> MapMessageAsync(
        Hl7Message message,
        string fileName,
        string facilityId,
        AdtProcessingContext context,
        CancellationToken cancellationToken)
    {
        var messageType = AdtEventMapper.GetMessageType(message);
        if (messageType is null)
        {
            LogSkippedEvent("it has no message type in MSH-9 or EVN-1", message, fileName, facilityId);
            return null;
        }

        var entry = AdtEventMapper.Map(message, messageType, context.MrnLocation, context.EncounterIdLocation);

        var patientId = context.PatientIdLocation is not null
            ? ReadPatientId(message, context.PatientIdLocation, fileName, facilityId)
            : await SearchPatientIdAsync(message, entry.MedicalRecordNumber, fileName, facilityId, context, cancellationToken);

        if (patientId is null)
        {
            return null;
        }

        entry.FhirPatientId = patientId;
        return entry;
    }

    private string? ReadPatientId(Hl7Message message, Hl7Path location, string fileName, string facilityId)
    {
        var value = message.GetValue(location);
        if (value is null)
        {
            LogSkippedEvent("no Patient.Id was found at the configured location", message, fileName, facilityId);
            return null;
        }

        var patientId = value.SplitReference().Trim();
        if (!FhirIdRegex().IsMatch(patientId))
        {
            LogSkippedEvent("the value at the configured Patient.Id location is not a valid FHIR id", message, fileName, facilityId);
            return null;
        }

        return patientId;
    }

    private async Task<string?> SearchPatientIdAsync(
        Hl7Message message,
        string? mrn,
        string fileName,
        string facilityId,
        AdtProcessingContext context,
        CancellationToken cancellationToken)
    {
        if (mrn is null)
        {
            LogSkippedEvent("no MRN was found at the configured location", message, fileName, facilityId);
            return null;
        }

        // A patient usually appears many times in a file: only search once for each MRN
        if (!context.SearchResults.TryGetValue(mrn, out var result))
        {
            result = await patientIdResolver.SearchByMrnAsync(
                facilityId, context.MrnIdentifierSystem!, mrn, context.QueryConfiguration!, cancellationToken);
            context.SearchResults[mrn] = result;
        }

        if (result.MatchCount == 0)
        {
            LogSkippedEvent("the Patient search by MRN returned no results", message, fileName, facilityId);
            return null;
        }

        if (result.MatchCount > 1)
        {
            LogSkippedEvent("the Patient search by MRN returned more than one result", message, fileName, facilityId);
            return null;
        }

        return result.PatientId;
    }

    private void LogSkippedEvent(string reason, Hl7Message message, string fileName, string facilityId)
    {
        logger.LogError(
            "ADT event not added to ADTAcquired for facility {FacilityId} because {Reason}. File: {FileName}; position: {Position}; message control id: {MessageControlId}; message type: {MessageType}",
            facilityId.SanitizeForLog(),
            reason,
            fileName.SanitizeForLog(),
            message.Position,
            AdtEventMapper.GetMessageControlId(message).SanitizeForLog(),
            AdtEventMapper.GetMessageType(message).SanitizeForLog());
    }

    /// <summary>
    /// Groups entries, in order, into batches that each serialize below the limit. Normally there is just one.
    /// A single entry above the limit still goes out alone, so that Kafka rejects it visibly.
    /// </summary>
    internal static List<List<AdtEventEntry>> SplitIntoMessages(List<AdtEventEntry> entries, int maxBytes)
    {
        var batches = new List<List<AdtEventEntry>>();
        var current = new List<AdtEventEntry>();
        var currentBytes = 0L;

        foreach (var entry in entries)
        {
            var entryBytes = JsonSerializer.SerializeToUtf8Bytes(entry).Length + 1;

            if (current.Count > 0 && currentBytes + entryBytes > maxBytes)
            {
                batches.Add(current);
                current = [];
                currentBytes = 0;
            }

            current.Add(entry);
            currentBytes += entryBytes;
        }

        if (current.Count > 0)
        {
            batches.Add(current);
        }

        return batches;
    }

    private sealed record AdtProcessingContext(
        Hl7Path? PatientIdLocation,
        string? MrnIdentifierSystem,
        FhirQueryConfigurationModel? QueryConfiguration,
        Hl7Path MrnLocation,
        Hl7Path EncounterIdLocation)
    {
        public Dictionary<string, AdtPatientSearchResult> SearchResults { get; } = new(StringComparer.Ordinal);
    }
}
