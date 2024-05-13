using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Submission.Application.Models;
using LantanaGroup.Link.Submission.Settings;
using MediatR;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Collections.Generic;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;
using System.Text.Json;
using Hl7.Fhir.Serialization;
using System.Text.Json;
using LantanaGroup.Link.Submission.Settings;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace LantanaGroup.Link.Submission.Listeners
{
    public class SubmitReportListener : BackgroundService
    {
        private readonly ILogger<SubmitReportListener> _logger;
        private readonly IKafkaConsumerFactory<SubmitReportKey, SubmitReportValue> _kafkaConsumerFactory;
        private readonly IMediator _mediator;
        private readonly SubmissionServiceConfig _submissionConfig;
        private readonly FileSystemConfig _fileSystemConfig;
        private readonly IHttpClientFactory _httpClient;

        private readonly ITransientExceptionHandler<SubmitReportKey, SubmitReportValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<SubmitReportKey, SubmitReportValue> _deadLetterExceptionHandler;

        private string Name => this.GetType().Name;

        public SubmitReportListener(ILogger<SubmitReportListener> logger, IKafkaConsumerFactory<SubmitReportKey, SubmitReportValue> kafkaConsumerFactory,
            IMediator mediator, IOptions<SubmissionServiceConfig> submissionConfig, IOptions<FileSystemConfig> fileSystemConfig, IHttpClientFactory httpClient,
            ITransientExceptionHandler<SubmitReportKey, SubmitReportValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<SubmitReportKey, SubmitReportValue> deadLetterExceptionHandler)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _mediator = mediator ?? throw new ArgumentException(nameof(mediator));
            _submissionConfig = submissionConfig.Value;
            _fileSystemConfig = fileSystemConfig.Value;
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(HttpClient));

            _transientExceptionHandler = transientExceptionHandler ??
                                         throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ??
                                          throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = "Submission";
            _transientExceptionHandler.Topic = nameof(KafkaTopic.SubmitReport) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = "Submission";
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.SubmitReport) + "-Error";
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig()
            {
                GroupId = SubmissionConstants.ServiceName,
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(config);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.SubmitReport));
                _logger.LogInformation($"Started consumer for topic '{nameof(KafkaTopic.SubmitReport)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var consumeResult = new ConsumeResult<SubmitReportKey, SubmitReportValue>();
                    string facilityId = string.Empty;
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                        {
                            consumeResult = result;

                            if (consumeResult == null)
                            {
                                throw new DeadLetterException($"{Name}: consumeResult is null", AuditEventType.Create);
                            }

                            var key = consumeResult.Message.Key;
                            var value = consumeResult.Message.Value;
                            facilityId = key.FacilityId;

                            if (string.IsNullOrWhiteSpace(key.FacilityId) ||
                                key.StartDate == null ||
                                key.EndDate == null)
                            {
                                throw new DeadLetterException(
                                    $"{Name}: One or more required Key/Value properties are null or empty.", AuditEventType.Create);
                            }

                            var httpClient = _httpClient.CreateClient();
                            string censusRequesturl = _submissionConfig.CensusAdmittedPatientsUrl + $"?facilityId={key.FacilityId}&startDate={key.StartDate}&endDate={key.EndDate}";
                            var censusResponse = await httpClient.GetAsync(censusRequesturl, cancellationToken);
                            var admittedPatients = JsonConvert.DeserializeObject<List<CensusAdmittedPatient>>(await censusResponse.Content.ReadAsStringAsync(cancellationToken));

                            string dataAcqRequestUrl = _submissionConfig.DataAcquisitionQueryPlanUrl + $"/{key.FacilityId}/QueryPlans";
                            var dataAcqResponse = await httpClient.GetAsync(censusRequesturl, cancellationToken);
                            var queryPlans = await censusResponse.Content.ReadAsStringAsync(cancellationToken);

                            Dictionary<string, Bundle> patientBundles = new Dictionary<string, Bundle>();
                            Bundle otherResourcesBundle = new Bundle();
                            var options = new JsonSerializerOptions().ForFhir();

                            foreach (var patientId in value.PatientIds.Distinct())
                            {
                                string requestUrl = _submissionConfig.ReportServiceUrl.Trim('/') +
                                                    $"/GetSubmissionBundleForPatient?FacilityId={facilityId}&PatientId={patientId}&StartDate={key.StartDate}&EndDate={key.EndDate}";
                                var response = await httpClient.GetAsync(requestUrl, cancellationToken);

                                var patientSubmissionBundle =
                                    JsonConvert.DeserializeObject<MeasureReportSubmissionModel>(
                                        await response.Content.ReadAsStringAsync(cancellationToken));

                                var patientBundle = System.Text.Json.JsonSerializer.Deserialize<Bundle>(patientSubmissionBundle.PatientResources, options);

                                patientBundles.TryAdd(patientId, patientBundle);

                                var otherResources = System.Text.Json.JsonSerializer.Deserialize<Bundle>(patientSubmissionBundle.OtherResources, options);
                                foreach (var resource in otherResources.GetResources())
                                {
                                    otherResourcesBundle.AddResourceEntry(resource, GetFullUrl(resource));
                                }
                            }

                            #region File IO

                            //Format: <nhsn-org-id>-<plus-separated-list-of-measure-ids>-<period-start>-<period-end?>-<timestamp>
                            //Per 2153, don't build with the trailing timestamp
                            string submissionDiretory = Path.Combine(_submissionConfig.SubmissionDirectory,
                                $"{facilityId}-{value.MeasureIds}-{key.StartDate.Value.ToShortDateString()}-{key.EndDate.Value.ToShortDateString()}");
                            if (Directory.Exists(submissionDiretory))
                            {
                                Directory.Delete(submissionDiretory, true);
                            }

                            Directory.CreateDirectory(submissionDiretory);

                            //Device
                            string fileName = "sending-device.json";
                            string contents = "{ \"Device\": \"NHSNLink\" }";

                            await File.WriteAllTextAsync(submissionDiretory + "/" + fileName, contents, cancellationToken);

                            //Organization
                            fileName = "sending-organization.json";
                            contents = System.Text.Json.JsonSerializer.Serialize(value.Organization, options);

                            await File.WriteAllTextAsync(submissionDiretory + "/" + fileName, contents, cancellationToken);

                            //Patient-List
                            fileName = "patient-list.json";
                            contents = System.Text.Json.JsonSerializer.Serialize(admittedPatients.Select(p => p.PatientId), options);

                            await File.WriteAllTextAsync(submissionDiretory + "/" + fileName, contents, cancellationToken);

                            //Query Plans
                            fileName = "query-plan.yml";
                            contents = queryPlans;

                            await File.WriteAllTextAsync(submissionDiretory + "/" + fileName, contents, cancellationToken);

                            //Aggregates
                            foreach (var aggregate in value.Aggregates)
                            {
                                fileName = $"aggregate-{aggregate.Measure}.json";
                                contents = System.Text.Json.JsonSerializer.Serialize(aggregate, options);

                                await File.WriteAllTextAsync(submissionDiretory + "/" + fileName, contents, cancellationToken);
                            }

                            //Patient Bundles
                            foreach (var bundle in patientBundles)
                            {
                                fileName = $"patient-{bundle.Key}.json";
                                contents = System.Text.Json.JsonSerializer.Serialize(bundle.Value, options);

                                await File.WriteAllTextAsync(submissionDiretory + "/" + fileName, contents, cancellationToken);
                            }

                            //Other Resources
                            fileName = $"other-resources.json";
                            contents = System.Text.Json.JsonSerializer.Serialize(otherResourcesBundle, options);

                            await File.WriteAllTextAsync(submissionDiretory + "/" + fileName, contents, cancellationToken);
                            
                            #endregion

                        }, cancellationToken);
                        
                    }
                    catch (ConsumeException ex)
                    {
                        _deadLetterExceptionHandler.HandleException(consumeResult,
                            new DeadLetterException($"{Name}: " + ex.Message, AuditEventType.Create, ex.InnerException), facilityId);
                    }
                    catch (DeadLetterException ex)
                    {
                        _deadLetterExceptionHandler.HandleException(consumeResult, ex, facilityId);
                    }
                    catch (TransientException ex)
                    {
                        _transientExceptionHandler.HandleException(consumeResult, ex, facilityId);
                    }
                    catch (Exception ex)
                    {
                        _deadLetterExceptionHandler.HandleException(consumeResult,
                            new DeadLetterException($"{Name}: " + ex.Message, AuditEventType.Query, ex.InnerException), facilityId);
                    }
                    finally
                    {
                        if (consumeResult != null)
                        {
                            consumer.Commit(consumeResult);
                        }
                        else
                        {
                            consumer.Commit();
                        }
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError(oce, $"Operation Canceled: {oce.Message}");
                consumer.Close();
                consumer.Dispose();
            }
        }

        protected string GetRelativeReference(Resource resource)
        {
            return string.Format("{0}/{1}", resource.TypeName, resource.Id);
        }

        protected string GetFullUrl(Resource resource)
        {
            return string.Format(SubmissionConstants.BundlingFullUrlFormat, GetRelativeReference(resource));
        }
    }
}
