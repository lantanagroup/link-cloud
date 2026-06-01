using System.Diagnostics;
using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using ListType = LantanaGroup.Link.Shared.Application.Models.DataAcq.ListType;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;
using TimeFrame = LantanaGroup.Link.Shared.Application.Models.DataAcq.TimeFrame;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
public interface IPatientCensusService
{
    Task CreateLog(string facilityId, CancellationToken cancellationToken);
    Task<List<PatientListItem>> RetrieveListData(DataAcquisitionLogModel log, bool triggerMessage, CancellationToken cancellationToken);
}

public class PatientCensusService : IPatientCensusService
{
    private readonly ILogger<PatientCensusService> _logger;
    private readonly IAuthenticationRetrievalService _authRetrievalService;
    private readonly IFhirQueryListConfigurationQueries _fhirQueryListConfigurationQueries;
    private readonly IReadFhirCommand _readFhirCommand;
    private readonly IFhirQueryConfigurationQueries _fhirQueryConfigurationQueries;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;
    private readonly IProducer<string, PatientListMessage> _kafkaProducer;
    private readonly ISftpConfigurationQueries _sftpConfigurationQueries;
    private readonly ISftpAcquisitionLogManager _sftpAcquisitionLogManager;

    public PatientCensusService(
        ILogger<PatientCensusService> logger,
        IAuthenticationRetrievalService authRetrievalService,
        IFhirQueryListConfigurationQueries fhirQueryListConfigurationQueries,
        IFhirQueryConfigurationQueries fhirQueryConfigurationQueries,
        IReadFhirCommand readFhirCommand,
        IDataAcquisitionLogManager dataAcquisitionLogManager,
        IProducer<string, PatientListMessage> kafkaProducer,
        ISftpConfigurationQueries sftpConfigurationQueries,
        ISftpAcquisitionLogManager sftpAcquisitionLogManager)
    {
        _logger = logger;
        _authRetrievalService = authRetrievalService;
        _readFhirCommand = readFhirCommand;
        _dataAcquisitionLogManager = dataAcquisitionLogManager;

        _fhirQueryListConfigurationQueries = fhirQueryListConfigurationQueries;
        _fhirQueryConfigurationQueries = fhirQueryConfigurationQueries;
        _kafkaProducer = kafkaProducer;
        _sftpConfigurationQueries = sftpConfigurationQueries;
        _sftpAcquisitionLogManager = sftpAcquisitionLogManager;
    }

    public async Task CreateLog(string facilityId, CancellationToken cancellationToken)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.FacilityId, facilityId);

        // Check if SFTP configuration exists for this facility
        var sftpConfig = await _sftpConfigurationQueries.GetByOrganizationIdAsync(facilityId, cancellationToken);

        if (sftpConfig is not null)
        {
            // Find all census-related acquisition configurations
            var censusConfigs = sftpConfig.AcquisitionConfigurations
                .Where(c => c.AcquisitionType == SftpAcquisitionType.Census)
                .ToList();

            if (censusConfigs.Count > 0)
            {
                foreach (var censusConfig in censusConfigs)
                {
                    // Create SftpAcquisitionLog for SFTP-based census
                    _logger.LogInformation(
                        "Facility {FacilityId} is configured for SFTP census acquisition (type: {AcquisitionType}, subType: {SubType})",
                        facilityId, censusConfig.AcquisitionType, censusConfig.SubType);

                    var sftpLog = new SftpAcquisitionLogModel
                    {
                        ExternalId = Guid.NewGuid(),
                        FacilityId = facilityId,
                        AcquisitionType = censusConfig.AcquisitionType,
                        SubType = censusConfig.SubType,
                        ScheduledDate = null,  // null = census should process immediately, can be set later for retry if needed
                        ProcessDate = null,
                        Status = RequestStatus.Pending,
                        OriginatingTraceId = Activity.Current?.TraceId.ToString(),
                        OriginatingSpanId = Activity.Current?.SpanId.ToString(),
                        Notes = [$"[{DateTime.UtcNow:O}] SFTP {censusConfig.AcquisitionType}/{censusConfig.SubType} acquisition scheduled"]
                    };

                    await _sftpAcquisitionLogManager.CreateAsync(sftpLog, cancellationToken);

                    _logger.LogInformation(
                        "Created SFTP acquisition log ({AcquisitionType}/{SubType}) for facility {FacilityId}",
                        censusConfig.AcquisitionType, censusConfig.SubType, facilityId);
                }

                return;
            }

            // SFTP config exists but no census acquisition configured - fall through to FHIR List
            _logger.LogDebug(
                "Facility {FacilityId} has SFTP config but no census acquisition configured, using FHIR List",
                facilityId);
        }

        // No SFTP configuration, continue with FHIR List logic
        var facilityConfig = await _fhirQueryListConfigurationQueries.GetByFacilityIdAsync(facilityId, cancellationToken);

        if (facilityConfig == null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Missing FHIR list configuration");
            throw new Exception(
                $"Missing census configuration for facility {facilityId}. Unable to proceed with request.");
        }

        var fhirQueryConfig = await _fhirQueryConfigurationQueries.GetByFacilityIdAsync(facilityConfig.FacilityId, cancellationToken);

        if (fhirQueryConfig == null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Missing FHIR query configuration");
            throw new Exception(
                $"Missing FHIR query configuration for facility {facilityId}. Unable to proceed with request.");
        }

        try
        {
            var log = new CreateDataAcquisitionLogModel
            {
                FacilityId = facilityId,
                Status = RequestStatus.Pending,
                QueryType = FhirQueryType.Read,
                ExecutionDate = DateTime.UtcNow,
                Priority = AcquisitionPriority.Normal,
                IsCensus = true,
            };

            facilityConfig.EHRPatientLists.ForEach(x =>
            {
                if (x.TimeFrame is null)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, "Timeframe is null for list");
                    activity?.AddTag("fhir.list.id", x.FhirId);
                    activity?.AddTag("fhir.list.internal.id", x.InternalId);
                    _logger.LogError("TimeFrame is null for list {listId} for facility {facilityId}.", x.FhirId, facilityId);
                }

                if (x.Status is null)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, "Status is null for list");
                    activity?.AddTag("fhir.list.id", x.FhirId);
                    activity?.AddTag("fhir.list.internal.id", x.InternalId);
                    _logger.LogError("Status is null for list {listId} for facility {facilityId}.", x.FhirId, facilityId);
                }

                log.FhirQuery.Add(
                    new CreateFhirQueryModel
                    {
                        FacilityId = facilityId,
                        QueryType = FhirQueryType.Read,
                        ResourceTypes = [ResourceType.List],
                        IsReference = false,
                        CensusTimeFrame = x.TimeFrame ?? throw new ArgumentNullException(nameof(x.TimeFrame)),
                        CensusPatientStatus = x.Status ?? throw new ArgumentNullException(nameof(x.Status)),
                        CensusListId = x.FhirId
                    });
            });

            await _dataAcquisitionLogManager.CreateAsync(log, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddTag(DiagnosticNames.StackTrace, ex.StackTrace);
            _logger.LogError(ex, "An error occurred while attempting to create the log entry. FacilityId: {facilityId}", facilityId);
            throw;
        }
    }

    public async Task<List<PatientListItem>> RetrieveListData(DataAcquisitionLogModel log, bool triggerMessage, CancellationToken cancellationToken)
    {
        List<PatientListItem> results = new List<PatientListItem>();

        if (log == null)
        {
            throw new ArgumentNullException(nameof(log), "Data acquisition log cannot be null.");
        }

        if (log.FhirQuery == null || log.FhirQuery.Count != 6)
        {
            throw new ArgumentException("Data acquisition log must contain exactly 6 FHIR query.", nameof(log));
        }

        List<string> notes = new List<string>();
        bool isFailed = false;

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        foreach (var query in log.FhirQuery)
        {
            if (query.QueryType != FhirQueryType.Read)
            {
                notes.Add($"Query type {query.QueryType} is not supported. Only Read queries are allowed.");
                continue;
            }

            if (query.ResourceTypes == null || !query.ResourceTypes.Contains(ResourceType.List))
            {
                notes.Add($"Resource type {query.ResourceTypes} is not supported. Only List resource type is allowed.");
                continue;
            }

            if (query.CensusPatientStatus == null)
            {
                notes.Add($"CensusPatientStatus is null for query with id {query.Id}. Unable to proceed with request.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(query.CensusListId))
            {
                notes.Add($"CensusListId is null or empty for query with id {query.Id}. Unable to proceed with request.");
                continue;
            }

            if (query.CensusTimeFrame == null)
            {
                notes.Add($"CensusTimeFrame is null for query with id {query.Id}. Unable to proceed with request.");
                continue;
            }

            var facilityConfig = await _fhirQueryListConfigurationQueries.GetByFacilityIdAsync(query.FacilityId, cancellationToken);
            if (facilityConfig == null)
            {
                throw new Exception(
                    $"Missing census configuration for facility {query.FacilityId}. Unable to proceed with request.");
            }

            (bool? isQueryParam, object? authHeader) authHeader = (false, null);
            if (facilityConfig.Authentication != null)
            {
                authHeader = await BuildeAuthHeader(query.FacilityId, facilityConfig.Authentication);
            }

            var fhirQueryConfig = await _fhirQueryConfigurationQueries.GetByFacilityIdAsync(facilityConfig.FacilityId);
            if (fhirQueryConfig == null)
            {
                throw new Exception(
                    $"Missing FHIR query configuration for facility {query.FacilityId}. Unable to proceed with request.");
            }

            try
            {
                var resultList = await _readFhirCommand.ExecuteAsync(
                    new ReadFhirCommandRequest(
                        query.FacilityId,
                        ResourceType.List,
                        query.CensusListId,
                        facilityConfig.FhirBaseServerUrl,
                        fhirQueryConfig,
                        log.ReportTrackingId),
                    cancellationToken);

                //check if the resultList is null or OperationOutcome
                if (resultList == null || resultList is OperationOutcome)
                {
                    throw new FhirApiFetchFailureException($"Error retrieving patient list id {query.CensusListId} for facility {facilityConfig.FacilityId}.");
                }

                var fhirList = resultList as List;
                results.Add(new PatientListItem
                {
                    ListType = ConvertToListType(query.CensusPatientStatus.Value),
                    TimeFrame = ConvertToTimeFrame(query.CensusTimeFrame.Value),
                    PatientIds = fhirList.Entry.Select(x => x.Item?.ReferenceElement.Value.SplitReference().Trim()).ToList() ?? [],
                });

            }
            catch (TimeoutException timeoutEx)
            {
                isFailed = true;
                notes.Add($"Timeout while retrieving patient list for facility {query.FacilityId} with list id {query.CensusListId}.");
            }
            catch (Exception ex)
            {
                isFailed = true;
                _logger.LogError(ex, "Error retrieving patient list for facility {FacilityId} with list id {CensusListId}", query.FacilityId.SanitizeForLog(), query.CensusListId.SanitizeForLog());
                notes.Add($"[{DateTime.UtcNow}] Error retrieving patient list for facility {query.FacilityId}. See application logs for details.");
            }
        }

        if (isFailed)
        {
            notes.Add($"[{DateTime.UtcNow}] Failed to retrieve patient list for facility {log.FacilityId}. See application logs for details.");
            log.Status = RequestStatus.Failed;
        }
        else
        {
            log.Status = RequestStatus.Completed;
        }

        stopwatch.Stop();

        log.CompletionTimeMilliseconds = stopwatch.ElapsedMilliseconds;
        log.CompletionDate = DateTime.UtcNow;
        var acquiredIds = results.SelectMany(x => x.PatientIds).ToList();

        await _dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
        {
            Id = log.Id,
            ResourceAcquiredIds = acquiredIds,
            RetryAttempts = log.RetryAttempts,
            CompletionDate = log.CompletionDate,
            CompletionTimeMilliseconds = log.CompletionTimeMilliseconds,
            ExecutionDate = log.ExecutionDate,
            NewNotes = notes.Count > 0 ? notes : null,
            Status = log.Status,
            TraceId = log.TraceId,
        }, cancellationToken);

        if (triggerMessage)
        {
            if (isFailed)
            {
                throw new Exception($"Failed to retrieve patient list for facility {log.FacilityId}. " + string.Join(", ", notes));
            }

            var produceMessage = new Message<string, PatientListMessage>
            {
                Key = log.FacilityId,
                Value = new PatientListMessage
                {
                    PatientLists = results,
                    ReportTrackingId = log.ReportTrackingId
                },
            };

            try
            {
                await _kafkaProducer.ProduceAsync(KafkaTopic.PatientListsAcquired.ToString(), produceMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while producing the message to Kafka for facility {facilityId} and log id {logid}.", log.FacilityId.SanitizeForLog(), log.Id.SanitizeForLog());
                throw;
            }
        }

        return results;
    }

    private async Task<(bool isQueryParam, object? authHeader)> BuildeAuthHeader(string facilityId, AuthenticationConfigurationModel auth)
    {
        (bool isQueryParam, object authHeader) authHeader = (false, null);
        IAuth authService = _authRetrievalService.GetAuthenticationService(auth);

        if (authService == null)
        {
            return (false, null);
        }

        authHeader = await authService.SetAuthentication(facilityId, auth);
        return authHeader;
    }

    private ListType ConvertToListType(Infrastructure.Models.Enums.ListType listType)
    {
        return listType switch
        {
            Infrastructure.Models.Enums.ListType.Admit => ListType.Admit,
            Infrastructure.Models.Enums.ListType.Discharge => ListType.Discharge,
            _ => throw new ArgumentOutOfRangeException(nameof(listType), $"Unsupported ListType value: {listType}"),
        };
    }

    private TimeFrame ConvertToTimeFrame(Infrastructure.Models.Enums.TimeFrame timeFrame)
    {
        return timeFrame switch
        {
            Infrastructure.Models.Enums.TimeFrame.LessThan24Hours => TimeFrame.LessThan24Hours,
            Infrastructure.Models.Enums.TimeFrame.Between24To48Hours => TimeFrame.Between24To48Hours,
            Infrastructure.Models.Enums.TimeFrame.MoreThan48Hours => TimeFrame.MoreThan48Hours,
            _ => throw new ArgumentOutOfRangeException(nameof(timeFrame), $"Unsupported TimeFrame value: {timeFrame}"),
        };
    }
}
