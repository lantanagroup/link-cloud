using Confluent.Kafka;
using DataAcquisition.Domain;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Application.Services;

public interface IPatientDataService
{
    Task Get(GetPatientDataRequest request, CancellationToken cancellationToken);
}

public class PatientDataService : IPatientDataService
{
    private readonly IDatabase _database;

    private readonly ILogger<PatientDataService> _logger;
    private readonly IFhirQueryConfigurationManager _fhirQueryManager;
    private readonly IQueryPlanManager _queryPlanManager;
    private readonly IFhirApiService _fhirRepo;
    private readonly IProducer<string, ResourceAcquired> _kafkaProducer;
    private readonly IQueryListProcessor _queryListProcessor;
    private readonly ProducerConfig _producerConfig;

    public PatientDataService(
        IDatabase database,
        ILogger<PatientDataService> logger,
        IFhirQueryConfigurationManager fhirQueryManager,
        IQueryPlanManager queryPlanManager,
        IFhirApiService fhirRepo,
        IProducer<string, ResourceAcquired> kafkaProducer,
        IQueryListProcessor queryListProcessor)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirQueryManager = fhirQueryManager ?? throw new ArgumentNullException(nameof(fhirQueryManager));
        _queryPlanManager = queryPlanManager ?? throw new ArgumentNullException(nameof(queryPlanManager));

        _fhirRepo = fhirRepo ?? throw new ArgumentNullException(nameof(fhirRepo));
        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));

        _producerConfig = new ProducerConfig();
        _producerConfig.CompressionType = CompressionType.Zstd;

        _queryListProcessor = queryListProcessor ?? throw new ArgumentNullException(nameof(queryListProcessor));
    }

    public async Task Get(GetPatientDataRequest request, CancellationToken cancellationToken)
    {
        var dataAcqRequested = request.ConsumeResult.Message.Value;

        FhirQueryConfiguration fhirQueryConfiguration = null;
        List<QueryPlan> queryPlans = null;

        try
        {
            fhirQueryConfiguration = await _fhirQueryManager.GetAsync(request.FacilityId, cancellationToken);
            queryPlans = await _queryPlanManager.FindAsync(q => q.FacilityId == request.FacilityId, cancellationToken);

            if (fhirQueryConfiguration == null || queryPlans == null)
            {
                throw new MissingFacilityConfigurationException(
                    $"No configuration for {request.FacilityId} exists.");
            }
        }
        catch (MissingFacilityConfigurationException ex)
        {
            var message =
                $"Error retrieving configuration for facility {request.FacilityId}\n{ex.Message}\n{ex.InnerException}";
            _logger.LogError(message);
            throw;
        }
        catch (Exception ex)
        {
            var message =
                $"Error retrieving configuration for facility {request.FacilityId}\n{ex.Message}\n{ex.InnerException}";
            _logger.LogError(message);
            throw;
        }

        Patient patient = null;
        var patientId = TEMPORARYPatientIdPart(dataAcqRequested.PatientId);

        if (dataAcqRequested.QueryType.Equals("Initial", StringComparison.InvariantCultureIgnoreCase))
        {
            patient = await _fhirRepo.GetPatient(
                fhirQueryConfiguration.FhirServerBaseUrl,
                patientId, request.CorrelationId,
                request.FacilityId,
                fhirQueryConfiguration.Authentication, cancellationToken);

            await _kafkaProducer.ProduceAsync(
                KafkaTopic.ResourceAcquired.ToString(),
                new Message<string, ResourceAcquired>
                {
                    Key = request.FacilityId,
                    Headers = new Headers
                    {
                            new Header(DataAcquisitionConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(request.CorrelationId))
                    },
                    Value = new ResourceAcquired
                    {
                        Resource = patient,
                        ScheduledReports = request.ConsumeResult.Value.ScheduledReports,
                        PatientId = patientId,
                        QueryType = dataAcqRequested.QueryType
                    }
                }, cancellationToken);
        }

        foreach (var scheduledReport in dataAcqRequested.ScheduledReports)
        {
            var queryPlan = queryPlans.FirstOrDefault(x => x.ReportType == scheduledReport.ReportType);
            
            if (queryPlan != null)
            {
                var initialQueries = queryPlan.InitialQueries.OrderBy(x => x.Key);
                var supplementalQueries = queryPlan.SupplementalQueries.OrderBy(x => x.Key);

                //var referenceTypes = queryPlan.InitialQueries.Where(x => x.GetType() == typeof(ReferenceQueryConfig)).Select(x => ((ReferenceQueryConfig)x.Value).ResourceType).ToList();
                var referenceTypes = queryPlan.InitialQueries.Values.OfType<ReferenceQueryConfig>().Select(x => x.ResourceType).Distinct().ToList();
                referenceTypes.AddRange(queryPlan.SupplementalQueries.Values.OfType<ReferenceQueryConfig>().Select(x => x.ResourceType).Distinct().ToList());

                try
                {
                    await _queryListProcessor.Process(
                            dataAcqRequested.QueryType.Equals("Initial", StringComparison.InvariantCultureIgnoreCase) ? initialQueries : supplementalQueries,
                            request,
                            fhirQueryConfiguration,
                            scheduledReport,
                            queryPlan,
                            referenceTypes,
                            dataAcqRequested.QueryType.Equals("Initial", StringComparison.InvariantCultureIgnoreCase) ? QueryPlanType.Initial.ToString() : QueryPlanType.Supplemental.ToString());

                }
                catch(ProduceException<string, ResourceAcquired>)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var message =
                        $"Error retrieving data from EHR for facility: {request.FacilityId}\n{ex.Message}\n{ex.InnerException}";
                    _logger.LogError(message);
                    throw;
                }
            }
        }
    }

    private static string TEMPORARYPatientIdPart(string fullPatientUrl)
    {
        var separatedPatientUrl = fullPatientUrl.Split('/');
        var patientIdPart = string.Join("/", separatedPatientUrl.Skip(Math.Max(0, separatedPatientUrl.Length - 2)));
        return patientIdPart;
    }
}
