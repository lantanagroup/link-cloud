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
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
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
        FhirQueryConfigurationModel fhirQueryConfiguration,
        ScheduledReport scheduledReport,
        QueryPlanModel queryPlan,
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
        foreach (var query in queryList)
        {
            var queryConfig = query.Value;

            QueryFactoryResult builtQuery = queryConfig switch
            {
                ParameterQueryConfig => ParameterQueryFactory.Build((ParameterQueryConfig)queryConfig, request, scheduledReport, queryPlan.LookBack),
                ReferenceQueryConfig => ReferenceQueryFactory.Build((ReferenceQueryConfig)queryConfig, new List<ResourceReference>()),
                _ => throw new Exception("Unable to identify type for query operation."),
            };

            _logger.LogInformation("Processing Query for:");

            var fhirQueryType = FhirQueryType.Read;

            var fhirQuery = new CreateFhirQueryModel
            {
                FacilityId = request.FacilityId,
                ResourceReferenceTypes = referenceTypes.Select(x => new CreateResourceReferenceTypeModel { FacilityId = x.FacilityId, QueryPhase = x.QueryPhase, ResourceType = x.ResourceType }).ToList(),
                MeasureId = scheduledReport.ReportTypes.FirstOrDefault(),
            };

            if (builtQuery is SingularParameterQueryFactoryResult)
            {
                var queryInfo = (ParameterQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {resourceType}", queryInfo.ResourceType);

                var factoryResult = (SingularParameterQueryFactoryResult)builtQuery;
                var config = (ParameterQueryConfig)queryConfig;

                var resourceType = Enum.Parse<ResourceType>(queryInfo.ResourceType);

                fhirQueryType = FhirQueryTypeUtilities.ToDomain(factoryResult.opType.ToString());
                fhirQuery.ResourceTypes = new List<ResourceType> { resourceType };
                fhirQuery.QueryParameters = factoryResult.SearchParams.Parameters.Select(x => $"{x.Item1}={x.Item2}").ToList();
                fhirQuery.QueryType = FhirQueryTypeUtilities.ToDomain(factoryResult.opType.ToString());
                fhirQuery.MeasureId = scheduledReport.ReportTypes.FirstOrDefault();
            }

            if (builtQuery is PagedParameterQueryFactoryResult)
            {
                var queryInfo = (ParameterQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {resourceType}", queryInfo.ResourceType);

                var factoryResult = (PagedParameterQueryFactoryResult)builtQuery;
                var config = (ParameterQueryConfig)queryConfig;

                fhirQueryType = FhirQueryTypeUtilities.ToDomain(factoryResult.opType.ToString());
                fhirQuery.ResourceTypes = new List<ResourceType> { Enum.Parse<ResourceType>(queryInfo.ResourceType) };
                fhirQuery.QueryParameters = factoryResult.SearchParamsList.SelectMany(y => y.Parameters.Select(x => $"{x.Item1}={x.Item2}")).ToList();
                fhirQuery.QueryType = FhirQueryTypeUtilities.ToDomain(factoryResult.opType.ToString());

            }

            if (builtQuery is ReferenceQueryFactoryResult)
            {
                var config = (ReferenceQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {resourceType}", config.ResourceType);

                fhirQueryType = FhirQueryTypeUtilities.ToDomain(config.OperationType.ToString());
                fhirQuery.QueryType = fhirQueryType;
                fhirQuery.ResourceTypes = [Enum.Parse<ResourceType>(config.ResourceType)];
                fhirQuery.QueryParameters = ["_id="];
                fhirQuery.ResourceReferenceTypes = [];
                fhirQuery.Paged = config.Paged;
                fhirQuery.IsReference = true;
            }

            await _dataAcquisitionLogManager.CreateAsync(new CreateDataAcquisitionLogModel
            {
                FacilityId = request.FacilityId,
                QueryType = fhirQueryType,
                Priority = AcquisitionPriority.Normal,
                PatientId = request.ConsumeResult.Value.PatientId,
                CorrelationId = request.CorrelationId,
                ReportableEvent = request.ConsumeResult.Value.ReportableEvent,
                FhirVersion = "R4",
                QueryPhase = QueryPhaseUtilities.ToDomain(request.QueryPlanType.ToString()),
                Status = RequestStatus.Pending,
                ScheduledReport = scheduledReport,
                ExecutionDate = DateTime.UtcNow,
                FhirQuery = new List<CreateFhirQueryModel>() { fhirQuery },
                TraceId = Activity.Current?.TraceId.ToHexString() ?? string.Empty,
            }, cancellationToken);
        }
    }
}
