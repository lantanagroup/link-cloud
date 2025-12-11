using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.DataAcq;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

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

    public PatientCensusService(
        ILogger<PatientCensusService> logger,
        IAuthenticationRetrievalService authRetrievalService,
        IFhirQueryListConfigurationQueries fhirQueryListConfigurationQueries,
        IFhirQueryConfigurationQueries fhirQueryConfigurationQueries,
        IReadFhirCommand readFhirCommand,
        IDataAcquisitionLogManager dataAcquisitionLogManager,
        IProducer<string, PatientListMessage> kafkaProducer)
    {
        _logger = logger;
        _authRetrievalService = authRetrievalService;
        _readFhirCommand = readFhirCommand;
        _dataAcquisitionLogManager = dataAcquisitionLogManager;

        _fhirQueryListConfigurationQueries = fhirQueryListConfigurationQueries;
        _fhirQueryConfigurationQueries = fhirQueryConfigurationQueries;
        _kafkaProducer = kafkaProducer;
    }

    public async Task CreateLog(string facilityId, CancellationToken cancellationToken)
    {
        List<PatientListModel> results = new List<PatientListModel>();
        var facilityConfig = await _fhirQueryListConfigurationQueries.GetByFacilityIdAsync(facilityId, cancellationToken);

        if (facilityConfig == null)
        {
            throw new Exception(
                $"Missing census configuration for facility {facilityId}. Unable to proceed with request.");
        }

        var fhirQueryConfig = await _fhirQueryConfigurationQueries.GetByFacilityIdAsync(facilityConfig.FacilityId);

        if (fhirQueryConfig == null)
        {
            throw new Exception(
                $"Missing FHIR query configuration for facility {facilityId}. Unable to proceed with request.");
        }

        List<List> resultLists = new List<List>();

        var log = new CreateDataAcquisitionLogModel
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            QueryType = FhirQueryType.Read,
            ExecutionDate = DateTime.UtcNow,
            Priority = AcquisitionPriority.Normal,
            IsCensus = true,
            ScheduledReport = new()
        };

        facilityConfig.EHRPatientLists.ForEach(x =>
        {
            log.FhirQuery.Add(
                new CreateFhirQueryModel
                {
                    FacilityId = facilityId,
                    QueryType = FhirQueryType.Read,
                    ResourceTypes = new List<ResourceType> { ResourceType.List },
                    IsReference = false,
                    CensusTimeFrame = x.TimeFrame,
                    CensusPatientStatus = x.Status,
                    CensusListId = x.FhirId
                });
        });

        await _dataAcquisitionLogManager.CreateAsync(log, cancellationToken);
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
                        fhirQueryConfig),
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
                notes.Add($"Error retrieving patient list for facility {query.FacilityId} with list id {query.CensusListId}: {ex.Message}");
            }
        }

        if (isFailed)
        {
            if (log.Notes == null)
            {
                log.Notes = new List<string>();
            }
            log.Notes.Add($"Failed to retrieve patient list for facility {log.FacilityId}. \n" + string.Join(", ", notes));
            log.Status = RequestStatus.Failed;
            log.Notes.AddRange(notes);
        }
        else
        {
            log.Status = RequestStatus.Completed;
        }

        stopwatch.Stop();

        log.CompletionTimeMilliseconds = stopwatch.ElapsedMilliseconds;
        log.CompletionDate = System.DateTime.UtcNow;
        log.ResourceAcquiredIds = results.SelectMany(x => x.PatientIds).ToList();

        // Ensure that the result of UpdateAsync is not null before assigning it to the log variable
        var updatedLog = await _dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
        {
            Id = log.Id,
            ResourceAcquiredIds = log.ResourceAcquiredIds,
            RetryAttempts = log.RetryAttempts,
            CompletionDate = log.CompletionDate,
            CompletionTimeMilliseconds = log.CompletionTimeMilliseconds,
            ExecutionDate = log.ExecutionDate,
            Notes = log.Notes,
            Status = log.Status,
            TraceId = log.TraceId,  
        }, cancellationToken);

        if (updatedLog == null)
        {
            throw new InvalidOperationException("Failed to update the DataAcquisitionLog. The returned value is null.");
        }

        log = updatedLog;

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
                _logger.LogError(ex, "An error occurred while producing the message to Kafka for facility {facilityId} and log id {logid}.", log.FacilityId, log.Id);
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

    private Shared.Application.Models.DataAcq.ListType ConvertToListType(Infrastructure.Models.Enums.ListType listType)
    {
        return listType switch
        {
            Infrastructure.Models.Enums.ListType.Admit => Shared.Application.Models.DataAcq.ListType.Admit,
            Infrastructure.Models.Enums.ListType.Discharge => Shared.Application.Models.DataAcq.ListType.Discharge,
            _ => throw new ArgumentOutOfRangeException(nameof(listType), $"Unsupported ListType value: {listType}"),
        };
    }

    private Shared.Application.Models.DataAcq.TimeFrame ConvertToTimeFrame(Infrastructure.Models.Enums.TimeFrame timeFrame)
    {
        return timeFrame switch
        {
            Infrastructure.Models.Enums.TimeFrame.LessThan24Hours => Shared.Application.Models.DataAcq.TimeFrame.LessThan24Hours,
            Infrastructure.Models.Enums.TimeFrame.Between24To48Hours => Shared.Application.Models.DataAcq.TimeFrame.Between24To48Hours,
            Infrastructure.Models.Enums.TimeFrame.MoreThan48Hours => Shared.Application.Models.DataAcq.TimeFrame.MoreThan48Hours,
            _ => throw new ArgumentOutOfRangeException(nameof(timeFrame), $"Unsupported TimeFrame value: {timeFrame}"),
        };
    }
}
