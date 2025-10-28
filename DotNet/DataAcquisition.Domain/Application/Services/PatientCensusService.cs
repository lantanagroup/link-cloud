using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
    public interface IPatientCensusService
    {
        Task CreateLog(string facilityId, CancellationToken cancellationToken);
        Task<List<PatientListModel>> RetrieveListData(DataAcquisitionLog log, bool triggerMessage, CancellationToken cancellationToken);
    }

    public class PatientCensusService : IPatientCensusService
    {
        private readonly ILogger<PatientCensusService> _logger;
        private readonly IAuthenticationRetrievalService _authRetrievalService;
        private readonly IFhirQueryListConfigurationManager _fhirQueryListConfigurationManager;
        private readonly IFhirApiService _fhirApiManager;
        private readonly IReadFhirCommand _readFhirCommand;
        private readonly IFhirQueryConfigurationManager _fhirQueryConfigurationManager;
        private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;
        private readonly IProducer<string, List<PatientListModel>> _kafkaProducer;

        public PatientCensusService(
            ILogger<PatientCensusService> logger,
            IAuthenticationRetrievalService authRetrievalService,
            IFhirQueryListConfigurationManager fhirQueryListConfigurationManager,
            IFhirApiService fhirApiManager
,
            IReadFhirCommand readFhirCommand,
            IFhirQueryConfigurationManager fhirQueryConfigurationManager,
            IDataAcquisitionLogManager dataAcquisitionLogManager,
            IProducer<string, List<PatientListModel>> kafkaProducer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authRetrievalService = authRetrievalService ?? throw new ArgumentNullException(nameof(authRetrievalService));
            _fhirQueryListConfigurationManager = fhirQueryListConfigurationManager ??
                                                 throw new ArgumentNullException(nameof(fhirQueryListConfigurationManager));
            _fhirApiManager = fhirApiManager ?? throw new ArgumentNullException(nameof(fhirApiManager));
            _readFhirCommand = readFhirCommand ?? throw new ArgumentNullException(nameof(readFhirCommand));
            _fhirQueryConfigurationManager = fhirQueryConfigurationManager ??
                                                 throw new ArgumentNullException(nameof(fhirQueryConfigurationManager));
            _dataAcquisitionLogManager = dataAcquisitionLogManager ??
                                                 throw new ArgumentNullException(nameof(dataAcquisitionLogManager));
            _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
        }

        public async Task CreateLog(string facilityId, CancellationToken cancellationToken)
        {
            List<PatientListModel> results = new List<PatientListModel>();
            var facilityConfig = await _fhirQueryListConfigurationManager.GetAsync(facilityId, cancellationToken);

            if (facilityConfig == null)
            {
                throw new Exception(
                    $"Missing census configuration for facility {facilityId}. Unable to proceed with request.");
            }

            var fhirQueryConfig = await _fhirQueryConfigurationManager.GetAsync(facilityConfig.FacilityId);

            if (fhirQueryConfig == null)
            {
                throw new Exception(
                    $"Missing FHIR query configuration for facility {facilityId}. Unable to proceed with request.");
            }

            List<List> resultLists = new List<List>();

            try
            {
                var log = new DataAcquisitionLog
                {
                    FacilityId = facilityId,
                    Status = RequestStatus.Pending,
                    QueryType = FhirQueryType.Read,
                    TimeZone = fhirQueryConfig.TimeZone,
                    ExecutionDate = DateTime.UtcNow,
                    Priority = AcquisitionPriority.Normal,
                    IsCensus = true,
                    ResourceAcquiredIds = new List<string>(),
                };
                facilityConfig.EHRPatientLists.ForEach(x =>
                {
                    log.FhirQuery.Add(
                        new FhirQuery
                        {
                            FacilityId = facilityId,
                            QueryType = FhirQueryType.Read,
                            ResourceTypes = new List<ResourceType> { ResourceType.List },
                            isReference = false,
                            CensusTimeFrame = x.TimeFrame,
                            CensusPatientStatus = x.Status,
                            CensusListId = x.FhirId
                        });
                });
                log = await _dataAcquisitionLogManager.CreateAsync(log, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while attempting to create the log entry. FacilityId: {facilityId}", facilityId);
                throw;
            }
        }

        public async Task<List<PatientListModel>> RetrieveListData(DataAcquisitionLog log, bool triggerMessage, CancellationToken cancellationToken)
        {
            List<PatientListModel> results = new List<PatientListModel>();

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
                    _logger.LogWarning("Query type {queryType} is not supported. Only Read queries are allowed.", query.QueryType);
                    continue;
                }

                if (query.ResourceTypes == null || !query.ResourceTypes.Contains(ResourceType.List))
                {
                    notes.Add($"Resource type {query.ResourceTypes} is not supported. Only List resource type is allowed.");
                    _logger.LogWarning("Resource type {ResourceTypes} is not supported. Only List resource type is allowed.", query.ResourceTypes);
                    continue;
                }

                if (query.CensusPatientStatus == null)
                {
                    notes.Add($"CensusPatientStatus is null for query with id {query.Id}. Unable to proceed with request.");
                    _logger.LogWarning("CensusPatientStatus is null for query with id {id}.", query.Id);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(query.CensusListId))
                {
                    notes.Add($"CensusListId is null or empty for query with id {query.Id}. Unable to proceed with request.");
                    _logger.LogWarning("CensusListId is null or empty for query with id {id}.", query.Id);
                    continue;
                }

                if (query.CensusTimeFrame == null)
                {
                    notes.Add($"CensusTimeFrame is null for query with id {query.Id}. Unable to proceed with request.");
                    _logger.LogWarning("CensusTimeFrame is null for query with id {id}.", query.Id);
                    continue;
                }

                var facilityConfig = await _fhirQueryListConfigurationManager.GetAsync(query.FacilityId, cancellationToken);
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

                var fhirQueryConfig = await _fhirQueryConfigurationManager.GetAsync(facilityConfig.FacilityId);
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
                        _logger.LogWarning("Error retrieving patient list id {ListId} for facility {FacilityId} with base url of {BaseUrl}.", query.CensusListId.Sanitize(), facilityConfig.FacilityId.Sanitize(), facilityConfig.FhirBaseServerUrl.Sanitize());
                        throw new FhirApiFetchFailureException($"Error retrieving patient list id {query.CensusListId} for facility {facilityConfig.FacilityId}.");
                    }

                    var fhirList = resultList as List;
                    if (fhirList != null && fhirList.Entry != null)
                    {
                        results.Add(new PatientListModel
                        {
                            ListType = query.CensusPatientStatus.Value,
                            TimeFrame = query.CensusTimeFrame.Value,
                            PatientIds =fhirList.Entry.Select(x => x.Item?.ReferenceElement.Value.SplitReference().Trim()).ToList(),
                        });
                    }
                }
                catch(TimeoutException timeoutEx)
                {
                    isFailed = true;
                    notes.Add($"Timeout while retrieving patient list for facility {query.FacilityId} with list id {query.CensusListId}.");
                    _logger.LogError(timeoutEx, "Timeout while retrieving patient list id {listId} for facility {facilityId}",
                        query.CensusListId, query.FacilityId);
                }
                catch (Exception ex)
                {
                    isFailed = true;
                    notes.Add($"Error retrieving patient list for facility {query.FacilityId} with list id {query.CensusListId}: {ex.Message}");
                    _logger.LogError(ex, "Error retrieving patient list id {listId} for facility {facilityId}",
                        query.CensusListId, query.FacilityId);
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
            var updatedLog = await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
            if (updatedLog == null)
            {
                throw new InvalidOperationException("Failed to update the DataAcquisitionLog. The returned value is null.");
            }
            log = updatedLog;

            log = await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);

            if (triggerMessage)
            {
                if (isFailed)
                {
                    throw new Exception($"Failed to retrieve patient list for facility {log.FacilityId}. " + string.Join(", ", notes));
                }

                var produceMessage = new Message<string, List<PatientListModel>>
                {
                    Key = log.FacilityId,
                    Value = results,
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

        private async Task<(bool isQueryParam, object? authHeader)> BuildeAuthHeader(string facilityId, AuthenticationConfiguration auth)
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
    }
