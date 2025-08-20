using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using System.Diagnostics;


namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public interface IReferenceResourceService
{
    Task<List<Resource>> FetchReferenceResources(
        ReferenceQueryFactoryResult referenceQueryFactoryResult,
        GetPatientDataRequest request,
        FhirQueryConfiguration fhirQueryConfiguration,
        ReferenceQueryConfig referenceQueryConfig,
        string queryPlanType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a collection of resource references and updates the specified data acquisition log accordingly.
    /// </summary>
    /// <remarks>This method processes the provided resource references and updates the log with relevant
    /// information. Ensure that the <paramref name="refResources"/> list contains valid references before calling this
    /// method.</remarks>
    /// <param name="log">The data acquisition log to be updated. This parameter cannot be <see langword="null"/>.</param>
    /// <param name="refResources">A list of resource references to process. This parameter can be <see langword="null"/> if no references were found.</param>
    /// <param name="cancellationToken">An optional token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ProcessReferences(DataAcquisitionLog log, List<ResourceReference> refResources, CancellationToken cancellationToken = default);
}

public class ReferenceResourceService : IReferenceResourceService
{
    private readonly ILogger<ReferenceResourceService> _logger;
    private readonly IReferenceResourcesManager _referenceResourcesManager;
    private readonly IProducer<string, ResourceAcquired> _kafkaProducer;
    private readonly IDataAcquisitionServiceMetrics _metrics;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;
    private readonly IDataAcquisitionLogQueries _dataAcquisitionLogQueries;
    private readonly IFhirQueryManager _fhirQueryMananger;


    public ReferenceResourceService(
        ILogger<ReferenceResourceService> logger,
        IReferenceResourcesManager referenceResourcesManager,
        IProducer<string, ResourceAcquired> kafkaProducer,
        IDataAcquisitionServiceMetrics metrics,
        IDataAcquisitionLogManager dataAcquisitionLogManager,
        IDataAcquisitionLogQueries dataAcquisitionLogQueries,
        IFhirQueryManager fhirQueryMananger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _referenceResourcesManager = referenceResourcesManager ?? throw new ArgumentNullException(nameof(referenceResourcesManager));
        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _dataAcquisitionLogManager = dataAcquisitionLogManager ?? throw new ArgumentNullException(nameof(dataAcquisitionLogManager));
        _dataAcquisitionLogQueries = dataAcquisitionLogQueries ?? throw new ArgumentNullException(nameof(dataAcquisitionLogQueries));
        _fhirQueryMananger = fhirQueryMananger ?? throw new ArgumentNullException(nameof(fhirQueryMananger));
    }

    public async Task<List<Resource>> FetchReferenceResources(ReferenceQueryFactoryResult referenceQueryFactoryResult, GetPatientDataRequest request, FhirQueryConfiguration fhirQueryConfiguration, ReferenceQueryConfig referenceQueryConfig, string queryPlanType, CancellationToken cancellationToken = default)
    {
        var resources = new List<Resource>();
        if (referenceQueryFactoryResult.ReferenceIds?.Count == 0)
        {
            return resources;
        }

        var validReferenceResources =
            referenceQueryFactoryResult
            ?.ReferenceIds
            ?.Where(x => x.TypeName == referenceQueryConfig.ResourceType || x.Reference.StartsWith(referenceQueryConfig.ResourceType, StringComparison.InvariantCultureIgnoreCase))
            .ToList();

        var existingReferenceResources =
            await _referenceResourcesManager.GetReferenceResourcesForListOfIds(
                validReferenceResources.Select(x => x.Reference.SplitReference()).ToList(),
                request.FacilityId);

        resources.AddRange(existingReferenceResources.Select(x => FhirResourceDeserializer.DeserializeFhirResource(x)));

        List<ResourceReference> missingReferences = validReferenceResources
            .Where(x => !existingReferenceResources.Any(y => y.ResourceId == x.Reference.SplitReference())).ToList();

        foreach (var x in missingReferences)
        {
            var fullMissingResources = new List<Resource>();
            resources.AddRange(fullMissingResources);
        }

        return resources;
    }


    public async Task ProcessReferences(DataAcquisitionLog log, List<ResourceReference> refResources, CancellationToken cancellationToken = default)
    {
        if (refResources == null || refResources.Count == 0)
            return;

        if (log == null)
            throw new ArgumentNullException(nameof(log), "Data acquisition log cannot be null.");

        //group refResources by type
        var groupedRefResources = refResources.Where(r => r.Url != null).GroupBy(r => r.Url.ToString().Split('/')[0]).ToList();

        _logger.LogInformation("Processing {Count} reference resources for log with ID: {LogId}", groupedRefResources.Sum(g => g.Count()), log.Id);

        foreach (var refResourcesTypeGroup in groupedRefResources)
        {
            var resourceType = refResourcesTypeGroup.Key;
            var refResourcesListDeDuped = refResourcesTypeGroup.DistinctBy(x => x.Url?.ToString()).ToList();
            // Get existing reference IDs async
            var existingRefIds = new HashSet<string>();
            foreach (var refResource in refResourcesListDeDuped)
            {
                var existing = await _referenceResourcesManager.GetByResourceIdAndFacilityId(refResource.Reference.SplitReference(), log.FacilityId, cancellationToken);
                if (existing != null && existing.ReferenceResource != null)
                {
                    existingRefIds.Add(refResource.Reference.SplitReference());
                }
            }

            var existingLog = await _dataAcquisitionLogQueries.GetLogByFacilityIdAndReportTrackingIdAndResourceType(log.FacilityId, log.ReportTrackingId, resourceType, log.CorrelationId, cancellationToken);

            // Filter out references that already exist
            var refResourceListForTypeFiltered = refResourcesListDeDuped.Where(x => !existingRefIds.Contains(x.Reference.SplitReference())).ToList();

            for (int i = 0; i < refResourcesListDeDuped.Count; i += 100)
            {
                //take chunks of 100 
                var chunk = refResourcesListDeDuped.Skip(i).Take(100).ToList();

                //if no chunk, continue to next iteration
                if (!chunk.Any()) continue;

                //get valid ids and normalize
                var idsList = string.Join(",", chunk.Select(x => x.Url.ToString().SplitReference()).Distinct());

                //check for existing log before adding a new one
                if (existingLog == null)
                {
                    var refResourcesTypes = //get all reference types from every log.FhirQuery and combine into 1 list
                    log.FhirQuery.SelectMany(x => x.ResourceReferenceTypes)
                        .Select(x => new ResourceReferenceType
                        {
                            ResourceType = x.ResourceType,
                            FacilityId = log.FacilityId,
                            QueryPhase = log.QueryPhase.Value,
                        })
                        .DistinctBy(x => x.ResourceType)
                        .ToList();

                    var newFhirQueries = new List<FhirQuery>
                        {
                            new FhirQuery
                            {
                                QueryType = FhirQueryType.Search,
                                FacilityId = log.FacilityId,
                                ResourceReferenceTypes = refResourcesTypes,
                                MeasureId = log.ScheduledReport?.ReportTypes.FirstOrDefault(),
                                ResourceTypes = new List<ResourceType> { Enum.Parse<ResourceType>(refResourcesTypeGroup.Key) },
                                QueryParameters = new List<string>
                                {
                                    $"_id={idsList}"
                                },
                            }
                        };

                    existingLog = CreateDataAcquisitionLog(log, refResourcesTypeGroup.Key, refResourcesTypes, newFhirQueries);

                    //add the log entry
                    await _dataAcquisitionLogManager.CreateAsync(existingLog, cancellationToken);
                }
                else
                {
                    //update existing log.FhirQUery with new parameters
                    var existingFhirQuery = existingLog.FhirQuery.FirstOrDefault(x => x.QueryType == FhirQueryType.Search && x.ResourceTypes.Contains(Enum.Parse<ResourceType>(refResourcesTypeGroup.Key)));
                    if (existingFhirQuery != null)
                    {
                        existingFhirQuery.QueryParameters = existingFhirQuery.QueryParameters.Union(new List<string> { $"_id={idsList}" }).ToList();
                        await _fhirQueryMananger.UpdateAsync(existingFhirQuery, cancellationToken);
                    }
                    else
                    {
                        //if no existing query, create a new one
                        var fhirQuery = new FhirQuery
                        {
                            QueryType = FhirQueryType.Search,
                            ResourceTypes = new List<ResourceType> { Enum.Parse<ResourceType>(refResourcesTypeGroup.Key) },
                            QueryParameters = new List<string> { $"_id={idsList}" },
                            MeasureId = log.ScheduledReport?.ReportTypes.FirstOrDefault(),
                            FacilityId = log.FacilityId,
                            DataAcquisitionLogId = log.Id,
                            ResourceReferenceTypes = new List<ResourceReferenceType>
                            {
                                new ResourceReferenceType
                                {
                                    FacilityId = log.FacilityId,
                                    QueryPhase = log.QueryPhase.Value,
                                    ResourceType = refResourcesTypeGroup.Key,
                                }
                            }
                        };

                        await _fhirQueryMananger.AddAsync(fhirQuery, cancellationToken);
                    }
                }
            }

            //save reference resources to db
            foreach (var refResource in refResourceListForTypeFiltered)
            {
                var parsedResourceType = refResource.Url.ToString().Split('/')[0];

                var referenceResource = new ReferenceResources
                {
                    ResourceId = refResource.Reference.SplitReference(),
                    ResourceType = parsedResourceType,
                    FacilityId = log.FacilityId,
                    DataAcquisitionLogId = log.Id,
                };
                await _referenceResourcesManager.AddAsync(referenceResource, cancellationToken);
            }
        }
    }

    private DataAcquisitionLog CreateDataAcquisitionLog(DataAcquisitionLog log, string resourceType, List<ResourceReferenceType> refResourcesTypes, List<FhirQuery> fhirQueries)
    {
        return new DataAcquisitionLog
        {
            FacilityId = log.FacilityId,
            Priority = log.Priority,
            PatientId = log.PatientId,
            CorrelationId = log.CorrelationId,
            ReportTrackingId = log.ReportTrackingId,
            ReportStartDate = log.ReportStartDate,
            ReportEndDate = log.ReportEndDate,
            ReportableEvent = log.ReportableEvent,
            FhirVersion = log.FhirVersion,
            QueryPhase = log.QueryPhase,
            QueryType = FhirQueryType.Search,
            Status = RequestStatus.Pending,
            TimeZone = log.TimeZone,
            ScheduledReport = log.ScheduledReport,
            ExecutionDate = DateTime.UtcNow,
            FhirQuery = fhirQueries,
            TraceId = log.TraceId
        };
    }

}
