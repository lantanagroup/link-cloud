using System.Diagnostics;
using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.QueryFactories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
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
        FhirQueryConfigurationModel fhirQueryConfiguration,
        ScheduledReport scheduledReport,
        QueryPlanModel queryPlan,
        List<string> referenceTypes,
        string queryPlanType,
        CancellationToken cancellationToken = default
        );

    Task Process(IOrderedEnumerable<KeyValuePair<string, IQueryConfig>> queryList,
        GetPatientDataRequest request,
        FhirQueryConfigurationModel fhirQueryConfiguration,
        QueryPlanModel queryPlan,
        List<ResourceReferenceType> referenceTypes,
        string queryPlanType,
        ScheduledReport scheduledReport,
        CancellationToken cancellationToken = default);
}

public class QueryListProcessor : IQueryListProcessor
{
    private readonly ILogger<QueryListProcessor> _logger;
    private readonly IFhirApiService _fhirRepo;
    private readonly IProducer<ResourceKey, ResourceAcquired> _kafkaProducer;
    private readonly IReferenceResourceService _referenceResourceService;
    private readonly ProducerConfig _producerConfig;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;
    private readonly IDataAcquisitionLogQueries _dataAcquisitionLogQueries;
    private readonly IParameterQueryFactory _parameterQueryFactory;

    public QueryListProcessor(
        ILogger<QueryListProcessor> logger,
        IFhirApiService fhirRepo,
        IProducer<ResourceKey, ResourceAcquired> kafkaProducer,
        IReferenceResourceService referenceResourceService,
        IDataAcquisitionLogManager dataAcquisitionLogManager,
        IDataAcquisitionLogQueries dataAcquisitionLogQueries,
        IParameterQueryFactory parameterQueryFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirRepo = fhirRepo ?? throw new ArgumentNullException(nameof(fhirRepo));
        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
        _referenceResourceService = referenceResourceService ?? throw new ArgumentNullException(nameof(referenceResourceService));
        _dataAcquisitionLogManager = dataAcquisitionLogManager ?? throw new ArgumentNullException(nameof(dataAcquisitionLogManager));
        _dataAcquisitionLogQueries = dataAcquisitionLogQueries ?? throw new ArgumentNullException(nameof(dataAcquisitionLogQueries));
        _parameterQueryFactory = parameterQueryFactory ?? throw new ArgumentNullException(nameof(parameterQueryFactory));

        _producerConfig = new ProducerConfig();
        _producerConfig.CompressionType = CompressionType.Zstd;
    }

    public async Task<List<Resource>> ExecuteFacilityValidationRequest(
        IOrderedEnumerable<KeyValuePair<string, IQueryConfig>> queryList,
        GetPatientDataRequest request,
        FhirQueryConfigurationModel fhirQueryConfiguration,
        ScheduledReport scheduledReport,
        QueryPlanModel queryPlan,
        List<string> referenceTypes,
        string queryPlanType,
        CancellationToken cancellationToken = default
        )
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("QueryListProcessor.ExecuteFacilityValidationRequest");
        activity?.SetTag(DiagnosticNames.FacilityId, request.FacilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, request.CorrelationId);
        activity?.SetTag(DiagnosticNames.QueryType, queryPlanType);

        var resources = new List<Resource>();
        List<ResourceReference> referenceResources = new List<ResourceReference>();
        foreach (var query in queryList)
        {
            var queryConfig = query.Value;

            if (queryConfig is ReferenceQueryConfig)
            {
                var referenceQueryFactoryResult = ReferenceQueryFactory.Build((ReferenceQueryConfig)queryConfig, new List<ResourceReference>());

                var queryInfo = (ReferenceQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {resourceType}", queryInfo.ResourceType);

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
        FhirQueryConfigurationModel fhirQueryConfiguration,
        QueryPlanModel queryPlan,
        List<ResourceReferenceType> referenceTypes,
        string queryPlanType,
        ScheduledReport scheduledReport,
        CancellationToken cancellationToken = default
        )
    {
        var traceId = Activity.Current?.TraceId.ToHexString();
        var spanId = Activity.Current?.SpanId.ToHexString();
        var traceAndSpanDelimited = traceId + "|" + spanId;

        foreach (var query in queryList)
        {
            var queryConfig = query.Value;
            List<string> resourceIds = null;

            QueryFactoryResult builtQuery = queryConfig switch
            {
                ParameterQueryConfig config => await _parameterQueryFactory.Build(config, request, scheduledReport, queryPlan.LookBack, _dataAcquisitionLogQueries),
                ReferenceQueryConfig config => ReferenceQueryFactory.Build(config, new List<ResourceReference>()),
                _ => throw new Exception("Unable to identify type for query operation."),
            };

            _logger.LogDebug("Processing Query for:");

            var fhirQuery = new CreateFhirQueryModel
            {
                FacilityId = request.FacilityId,
                ResourceReferenceTypes = referenceTypes.Select(x => new CreateResourceReferenceTypeModel { FacilityId = x.FacilityId, QueryPhase = x.QueryPhase, ResourceType = x.ResourceType }).ToList(),
                MeasureId = scheduledReport.ReportTypes.FirstOrDefault(),
            };

            if (builtQuery is SingularParameterQueryFactoryResult)
            {
                var queryInfo = (ParameterQueryConfig)queryConfig;
                _logger.LogDebug("Resource: {resourceType}", queryInfo.ResourceType);

                var factoryResult = (SingularParameterQueryFactoryResult)builtQuery;
                var resourceType = Enum.Parse<ResourceType>(queryInfo.ResourceType);

                fhirQuery.ResourceTypes = [resourceType];
                fhirQuery.QueryParameters = factoryResult.SearchParams?.Select(x => $"{x.Key}={x.Value}").ToList() ?? [];
                fhirQuery.QueryType = FhirQueryTypeUtilities.ToDomain(factoryResult.opType.ToString());
                fhirQuery.MeasureId = scheduledReport.ReportTypes.FirstOrDefault();

                await CreateDataAcquisitionLogAsync(
                    request,
                    FhirQueryTypeUtilities.ToDomain(factoryResult.opType.ToString()),
                    scheduledReport,
                    fhirQuery,
                    traceAndSpanDelimited,
                    cancellationToken);
            }
            else if (builtQuery is PagedParameterQueryFactoryResult)
            {
                var queryInfo = (ParameterQueryConfig)queryConfig;
                _logger.LogDebug("Resource: {resourceType}", queryInfo.ResourceType);

                var factoryResult = (PagedParameterQueryFactoryResult)builtQuery;

                foreach (var searchParams in factoryResult.SearchParamsList)
                {
                    var pagedFhirQuery = new CreateFhirQueryModel
                    {
                        FacilityId = request.FacilityId,
                        ResourceReferenceTypes = referenceTypes.Select(x => new CreateResourceReferenceTypeModel { FacilityId = x.FacilityId, QueryPhase = x.QueryPhase, ResourceType = x.ResourceType }).ToList(),
                        MeasureId = scheduledReport.ReportTypes.FirstOrDefault(),
                        ResourceTypes = new List<ResourceType> { Enum.Parse<ResourceType>(queryInfo.ResourceType) },
                        QueryParameters = searchParams.Select(x => $"{x.Key}={x.Value}").ToList(),
                        QueryType = FhirQueryTypeUtilities.ToDomain(factoryResult.opType.ToString())
                    };

                    await CreateDataAcquisitionLogAsync(
                        request,
                        FhirQueryTypeUtilities.ToDomain(factoryResult.opType.ToString()),
                        scheduledReport,
                        pagedFhirQuery,
                        traceAndSpanDelimited,
                        cancellationToken);
                }
            }
            else if (builtQuery is ReferenceQueryFactoryResult)
            {
                var config = (ReferenceQueryConfig)queryConfig;
                _logger.LogDebug("Resource: {resourceType}", config.ResourceType);

                var fhirQueryType = FhirQueryTypeUtilities.ToDomain(config.OperationType.ToString());
                fhirQuery.QueryType = fhirQueryType;
                fhirQuery.ResourceTypes = [Enum.Parse<ResourceType>(config.ResourceType)];
                fhirQuery.QueryParameters = ["_id="];
                fhirQuery.ResourceReferenceTypes = [];
                fhirQuery.Paged = config.Paged;
                fhirQuery.IsReference = true;

                await CreateDataAcquisitionLogAsync(
                    request,
                    fhirQueryType,
                    scheduledReport,
                    fhirQuery,
                    traceAndSpanDelimited,
                    cancellationToken);
            }
        }
    }

    private async Task CreateDataAcquisitionLogAsync(
        GetPatientDataRequest request,
        FhirQueryType fhirQueryType,
        ScheduledReport scheduledReport,
        CreateFhirQueryModel fhirQuery,
        string traceAndSpanDelimited,
        CancellationToken cancellationToken)
    {
        await _dataAcquisitionLogManager.CreateAsync(new CreateDataAcquisitionLogModel
        {
            FacilityId = request.FacilityId,
            QueryType = fhirQueryType,
            Priority = AcquisitionPriority.Normal,
            PatientId = request.ConsumeResult.Message.Value.PatientId,
            CorrelationId = request.CorrelationId,
            ReportableEvent = request.ConsumeResult.Message.Value.ReportableEvent,
            FhirVersion = "R4",
            QueryPhase = QueryPhaseUtilities.ToDomain(request.QueryPlanType.ToString()),
            Status = RequestStatus.Pending,
            ScheduledReport = scheduledReport,
            ExecutionDate = DateTime.UtcNow,
            FhirQuery = [fhirQuery],
            TraceId = traceAndSpanDelimited ?? string.Empty,
        }, cancellationToken);
    }
}
