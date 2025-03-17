using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Application.Factories.QueryFactories;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Application.Services;

public interface IQueryListProcessor
{
    Task<List<Resource>> Process_NoKafka(
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
        List<string> referenceTypes,
        string queryPlanType, 
        CancellationToken cancellationToken = default);
}

public class QueryListProcessor : IQueryListProcessor
{
    private readonly ILogger<QueryListProcessor> _logger;
    private readonly IFhirApiService _fhirRepo;
    private readonly IProducer<string, ResourceAcquired> _kafkaProducer;
    private readonly IReferenceResourceService _referenceResourceService;
    private readonly ProducerConfig _producerConfig;

    public QueryListProcessor(
        ILogger<QueryListProcessor> logger,
        IFhirApiService fhirRepo,
        IProducer<string, ResourceAcquired> kafkaProducer,
        IReferenceResourceService referenceResourceService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirRepo = fhirRepo ?? throw new ArgumentNullException(nameof(fhirRepo));
        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
        _referenceResourceService = referenceResourceService ?? throw new ArgumentNullException(nameof(referenceResourceService));

        _producerConfig = new ProducerConfig();
        _producerConfig.CompressionType = CompressionType.Zstd;
    }

    public async Task<List<Resource>> Process_NoKafka(
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

                var bundle = await _fhirRepo.GetSingularBundledResultsAsync(
                    fhirQueryConfiguration.FhirServerBaseUrl,
                    request.ConsumeResult.Message.Value.PatientId,
                    request.CorrelationId,
                    request.FacilityId,
                    queryPlanType,
                    (SingularParameterQueryFactoryResult)builtQuery,
                    (ParameterQueryConfig)queryConfig,
                    scheduledReport,
                    fhirQueryConfiguration.Authentication);

                referenceResources.AddRange(ReferenceResourceBundleExtractor.Extract(bundle, referenceTypes));
                resources.AddRange(bundle.Entry.Select(e => e.Resource));
            }

            if (builtQuery.GetType() == typeof(PagedParameterQueryFactoryResult))
            {
                var queryInfo = (ParameterQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {1}", queryInfo.ResourceType);

                var bundle = await _fhirRepo.GetPagedBundledResultsAsync(
                    fhirQueryConfiguration.FhirServerBaseUrl,
                    request.ConsumeResult.Message.Value.PatientId,
                    request.CorrelationId,
                    request.FacilityId,
                    queryPlanType,
                    (PagedParameterQueryFactoryResult)builtQuery,
                    (ParameterQueryConfig)queryConfig,
                    scheduledReport,
                    fhirQueryConfiguration.Authentication);

                referenceResources.AddRange(ReferenceResourceBundleExtractor.Extract(bundle, referenceTypes));
                resources.AddRange(bundle.Entry.Select(e => e.Resource));
            }

            if (builtQuery.GetType() == typeof(ReferenceQueryFactoryResult))
            {
                var referenceQueryFactoryResult = (ReferenceQueryFactoryResult)builtQuery;

                var queryInfo = (ReferenceQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {1}", queryInfo.ResourceType);

                var results = await _referenceResourceService.Execute_NoKafka(
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
        List<string> referenceTypes,
        string queryPlanType,
        CancellationToken cancellationToken = default
        )
    {
        List<ResourceReference> referenceResources = new List<ResourceReference>();
        var scheduledReport = GetScheduledReport(request.ConsumeResult.Message.Value.ScheduledReports);

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

            if (builtQuery.GetType() == typeof(SingularParameterQueryFactoryResult))
            {
                var queryInfo = (ParameterQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {1}", queryInfo.ResourceType);

                var references = await _fhirRepo.GetSingularBundledResultsAndGenerateMessagesAsync(
                    fhirQueryConfiguration.FhirServerBaseUrl,
                    request,
                    queryPlanType,
                    referenceTypes,
                    (SingularParameterQueryFactoryResult)builtQuery,
                    (ParameterQueryConfig)queryConfig,
                    fhirQueryConfiguration.Authentication);

                referenceResources.AddRange(references);
            }

            if (builtQuery.GetType() == typeof(PagedParameterQueryFactoryResult))
            {
                var queryInfo = (ParameterQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {1}", queryInfo.ResourceType);

                var references = await _fhirRepo.GetPagedBundledResultAndGenerateMessagesAsync(
                    fhirQueryConfiguration.FhirServerBaseUrl,
                    request,
                    queryPlanType,
                    referenceTypes,
                    (PagedParameterQueryFactoryResult)builtQuery,
                    (ParameterQueryConfig)queryConfig,
                    fhirQueryConfiguration.Authentication);

                referenceResources.AddRange(references);
            }

            if (builtQuery.GetType() == typeof(ReferenceQueryFactoryResult))
            {
                var referenceQueryFactoryResult = (ReferenceQueryFactoryResult)builtQuery;

                var queryInfo = (ReferenceQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {1}", queryInfo.ResourceType);

                await _referenceResourceService.Execute(
                    referenceQueryFactoryResult,
                    request,
                    fhirQueryConfiguration,
                    queryInfo,
                    queryPlanType);
            }

        }
    }

    private bool RemovePatientId(Resource resource)
    {
        return resource switch
        {
            Device => true,
            Medication => true,
            Location => true,
            Specimen => true,
            _ => false,
        };
    }

    private ScheduledReport GetScheduledReport(List<ScheduledReport> scheduledReports)
    {
        return scheduledReports.OrderByDescending(x => (int)x.Frequency).ToList().FirstOrDefault();
    }
}
