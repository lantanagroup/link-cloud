using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.QueryFactories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public interface IQueryListProcessor
{
    Task<List<Resource>> ExecuteFacilityValidationRequest(
        IOrderedEnumerable<KeyValuePair<string, IQueryConfig>> queryList,
        GetPatientDataRequest request,
        FhirQueryConfiguration fhirQueryConfiguration,
        ScheduledReport scheduledReport,
        QueryPlan queryPlan,
        List<string> referenceTypes,
        string queryPlanType,
        CancellationToken cancellationToken = default
        );

    Task Process(IOrderedEnumerable<KeyValuePair<string, IQueryConfig>> queryList,
        GetPatientDataRequest request,
        FhirQueryConfiguration fhirQueryConfiguration,
        QueryPlan queryPlan,
        List<ResourceReferenceType> referenceTypes,
        string queryPlanType,
        ScheduledReport scheduledReport,
        CancellationToken cancellationToken = default);
}

public class QueryListProcessor : IQueryListProcessor
{
    private readonly ILogger<QueryListProcessor> _logger;
    private readonly IFhirApiService _fhirRepo;
    private readonly IProducer<string, ResourceAcquired> _kafkaProducer;
    private readonly IReferenceResourceService _referenceResourceService;
    private readonly ProducerConfig _producerConfig;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;

    public QueryListProcessor(
        ILogger<QueryListProcessor> logger,
        IFhirApiService fhirRepo,
        IProducer<string, ResourceAcquired> kafkaProducer,
        IReferenceResourceService referenceResourceService,
        IDataAcquisitionLogManager dataAcquisitionLogManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirRepo = fhirRepo ?? throw new ArgumentNullException(nameof(fhirRepo));
        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
        _referenceResourceService = referenceResourceService ?? throw new ArgumentNullException(nameof(referenceResourceService));
        _dataAcquisitionLogManager = dataAcquisitionLogManager ?? throw new ArgumentNullException(nameof(dataAcquisitionLogManager));

        _producerConfig = new ProducerConfig();
        _producerConfig.CompressionType = CompressionType.Zstd;
    }

    public async Task<List<Resource>> ExecuteFacilityValidationRequest(
        IOrderedEnumerable<KeyValuePair<string, IQueryConfig>> queryList,
        GetPatientDataRequest request,
        FhirQueryConfiguration fhirQueryConfiguration,
        ScheduledReport scheduledReport,
        QueryPlan queryPlan,
        List<string> referenceTypes,
        string queryPlanType,
        CancellationToken cancellationToken = default
        )
    {
        var resources = new List<Resource>();
        List<ResourceReference> referenceResources = new List<ResourceReference>();
        foreach (var query in queryList)
        {
            var queryConfig = query.Value;
            QueryFactoryResult builtQuery = queryConfig switch
            {
                ParameterQueryConfig => ParameterQueryFactory.Build((ParameterQueryConfig)queryConfig, request,
                    scheduledReport, queryPlan.LookBack),
                ReferenceQueryConfig => ReferenceQueryFactory.Build((ReferenceQueryConfig)queryConfig, referenceResources),
                _ => throw new Exception("Unable to identify type for query operation."),
            };

            _logger.LogInformation("Processing Query for {QueryType}", builtQuery.GetType().Name);

            if (builtQuery.GetType() == typeof(SingularParameterQueryFactoryResult))
            {
                var queryInfo = (ParameterQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {1}", queryInfo.ResourceType);

                Bundle? bundle = null;
                    //await _fhirRepo.GetSingularBundledResultsAsync(
                    //fhirQueryConfiguration.FhirServerBaseUrl,
                    //request.ConsumeResult.Message.Value.PatientId,
                    //request.CorrelationId,
                    //request.FacilityId,
                    //queryPlanType,
                    //(SingularParameterQueryFactoryResult)builtQuery,
                    //(ParameterQueryConfig)queryConfig,
                    //scheduledReport,
                    //fhirQueryConfiguration.Authentication);

                referenceResources.AddRange(ReferenceResourceBundleExtractor.Extract(bundle, referenceTypes));
                resources.AddRange(bundle.Entry.Select(e => e.Resource));
            }

            if (builtQuery.GetType() == typeof(PagedParameterQueryFactoryResult))
            {
                var queryInfo = (ParameterQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {1}", queryInfo.ResourceType);

                Bundle? bundle = null;
                    //await _fhirRepo.GetPagedBundledResultsAsync(
                    //fhirQueryConfiguration.FhirServerBaseUrl,
                    //request.ConsumeResult.Message.Value.PatientId,
                    //request.CorrelationId,
                    //request.FacilityId,
                    //queryPlanType,
                    //(PagedParameterQueryFactoryResult)builtQuery,
                    //(ParameterQueryConfig)queryConfig,
                    //scheduledReport,
                    //fhirQueryConfiguration.Authentication);

                referenceResources.AddRange(ReferenceResourceBundleExtractor.Extract(bundle, referenceTypes));
                resources.AddRange(bundle.Entry.Select(e => e.Resource));
            }

            if (builtQuery.GetType() == typeof(ReferenceQueryFactoryResult))
            {
                var referenceQueryFactoryResult = (ReferenceQueryFactoryResult)builtQuery;

                var queryInfo = (ReferenceQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {1}", queryInfo.ResourceType);

                var results = await _referenceResourceService.FetchReferenceResources(
                    referenceQueryFactoryResult,
                    request,
                    fhirQueryConfiguration,
                    queryInfo,
                    queryPlanType);

                resources.AddRange(results);
            }
        }

        return resources;
    }

    public async Task Process(
        IOrderedEnumerable<KeyValuePair<string, IQueryConfig>> queryList,
        GetPatientDataRequest request,
        FhirQueryConfiguration fhirQueryConfiguration,
        QueryPlan queryPlan,
        List<ResourceReferenceType> referenceTypes,
        string queryPlanType,
        ScheduledReport scheduledReport,
        CancellationToken cancellationToken = default
        )
    {
        List<ResourceReference> referenceResources = new List<ResourceReference>();

        foreach (var query in queryList)
        {
            var queryConfig = query.Value;
            QueryFactoryResult builtQuery = queryConfig switch
            {
                ParameterQueryConfig => ParameterQueryFactory.Build((ParameterQueryConfig)queryConfig, request,
                    scheduledReport, queryPlan.LookBack, referenceResources.Select(x => x.Reference.SplitReference()).Distinct().ToList()),
                ReferenceQueryConfig => ReferenceQueryFactory.Build((ReferenceQueryConfig)queryConfig, referenceResources),
                _ => throw new Exception("Unable to identify type for query operation."),
            };

            _logger.LogInformation("Processing Query for:");

            var log = new DataAcquisitionLog
            {
                FacilityId = request.FacilityId,
                Priority = AcquisitionPriority.Normal,
                PatientId = request.ConsumeResult.Value.PatientId,
                CorrelationId = request.CorrelationId,
                ReportTrackingId = scheduledReport.ReportTrackingId,
                ReportStartDate = scheduledReport.StartDate,
                ReportEndDate = scheduledReport.EndDate,
                //ReportableEvent = ReportableEventToQueryPlanTypeFactory.GenerateReportableEventFromQueryPlanType(scheduledReport.Frequency),
                ReportableEvent = request.ConsumeResult.Value.ReportableEvent,
                FhirVersion = "R4",
                QueryPhase = QueryPhaseUtilities.ToDomain(request.QueryPlanType.ToString()),
                Status = RequestStatus.Pending,
                TimeZone = "UTC",
                ScheduledReport = scheduledReport,
                ExecutionDate = DateTime.UtcNow,
                FhirQuery = new List<FhirQuery>
                {

                },
            };

            var fhirQuery = new FhirQuery
            {
                FacilityId = request.FacilityId,
                ResourceReferenceTypes = referenceTypes.Select(x => new ResourceReferenceType { FacilityId = x.FacilityId, QueryPhase = x.QueryPhase, ResourceType = x.ResourceType }).ToList(),
                MeasureId = scheduledReport.ReportTypes.FirstOrDefault(),
            };

            if (builtQuery.GetType() == typeof(SingularParameterQueryFactoryResult))
            {
                var queryInfo = (ParameterQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {1}", queryInfo.ResourceType);

                var factoryResult = (SingularParameterQueryFactoryResult)builtQuery;
                var config = (ParameterQueryConfig)queryConfig;

                var resourceType = Enum.Parse<ResourceType>(queryInfo.ResourceType);

                log.QueryType = FhirQueryTypeUtilities.ToDomain(factoryResult.opType.ToString());
                fhirQuery.ResourceTypes = new List<ResourceType> { resourceType };
                fhirQuery.QueryParameters = factoryResult.SearchParams.Parameters.Select(x => $"{x.Item1}={x.Item2}").ToList();
                fhirQuery.QueryType = FhirQueryTypeUtilities.ToDomain(factoryResult.opType.ToString());
                fhirQuery.MeasureId = scheduledReport.ReportTypes.FirstOrDefault();
            }

            if (builtQuery.GetType() == typeof(PagedParameterQueryFactoryResult))
            {
                var queryInfo = (ParameterQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {1}", queryInfo.ResourceType);

                var factoryResult = (PagedParameterQueryFactoryResult)builtQuery;
                var config = (ParameterQueryConfig)queryConfig;

                log.QueryType = FhirQueryTypeUtilities.ToDomain(factoryResult.opType.ToString());
                fhirQuery.ResourceTypes = new List<ResourceType> { Enum.Parse<ResourceType>(queryInfo.ResourceType) };
                fhirQuery.QueryParameters = factoryResult.SearchParamsList.SelectMany(y => y.Parameters.Select(x => $"{x.Item1}={x.Item2}")).ToList();
                fhirQuery.QueryType = FhirQueryTypeUtilities.ToDomain(factoryResult.opType.ToString());

            }

            if (builtQuery.GetType() == typeof(ReferenceQueryFactoryResult))
            {
                //do nothing, reference queries are handled separately
                return;
            }

            log.FhirQuery.Add(fhirQuery);
            await _dataAcquisitionLogManager.CreateAsync(log, cancellationToken);
        }
    }

    private ScheduledReport GetScheduledReport(List<ScheduledReport> scheduledReports)
    {
        return scheduledReports.OrderByDescending(x => (int)x.Frequency).ToList().FirstOrDefault();
    }
}
