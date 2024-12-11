using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Settings;
using LantanaGroup.Link.Submission.Application.Config;
using LantanaGroup.Link.Submission.Application.Models;
using LantanaGroup.Link.Submission.Settings;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Submission.Listeners
{
    public class SubmitReportListener : BackgroundService
    {
        private readonly ILogger<SubmitReportListener> _logger;
        private readonly IKafkaConsumerFactory<SubmitReportKey, SubmitReportValue> _kafkaConsumerFactory;
        private readonly SubmissionServiceConfig _submissionConfig;
        private readonly IHttpClientFactory _httpClient;
        private readonly ServiceRegistry _serviceRegistry;

        private readonly ITransientExceptionHandler<SubmitReportKey, SubmitReportValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<SubmitReportKey, SubmitReportValue> _deadLetterExceptionHandler;

        private readonly IOptions<LinkTokenServiceSettings> _linkTokenServiceConfig;
        private readonly ICreateSystemToken _createSystemToken;

        private string Name => this.GetType().Name;

        public SubmitReportListener(ILogger<SubmitReportListener> logger,
            IKafkaConsumerFactory<SubmitReportKey, SubmitReportValue> kafkaConsumerFactory,
            IOptions<SubmissionServiceConfig> submissionConfig,
            IHttpClientFactory httpClient,
            ITransientExceptionHandler<SubmitReportKey, SubmitReportValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<SubmitReportKey, SubmitReportValue> deadLetterExceptionHandler,
            IOptions<LinkTokenServiceSettings> linkTokenServiceConfig, ICreateSystemToken createSystemToken, 
            IOptions<ServiceRegistry> serviceRegistry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _submissionConfig = submissionConfig.Value;
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(HttpClient));

            _transientExceptionHandler = transientExceptionHandler ??
                                         throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ??
                                          throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = "Submission";
            _transientExceptionHandler.Topic = nameof(KafkaTopic.SubmitReport) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = "Submission";
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.SubmitReport) + "-Error";

            _linkTokenServiceConfig = linkTokenServiceConfig ?? throw new ArgumentNullException(nameof(linkTokenServiceConfig));
            _createSystemToken = createSystemToken ?? throw new ArgumentNullException(nameof(createSystemToken));
            
            _serviceRegistry = serviceRegistry?.Value ?? throw new ArgumentNullException(nameof(serviceRegistry));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private string GetMeasureShortName(string measure)
        {
            // If a URL, may contain |0.1.2 representing the version at the end of the URL
            // Remove it so that we're looking at the generic URL, not the URL specific to a measure version
            string measureWithoutVersion = measure.Contains("|") ?
                measure.Substring(0, measure.LastIndexOf("|", StringComparison.Ordinal)) :
                measure;

            var urlShortName = _submissionConfig.MeasureNames.FirstOrDefault(x => x.Url == measureWithoutVersion || x.MeasureId == measureWithoutVersion)?.ShortName;

            if (!string.IsNullOrWhiteSpace(urlShortName))
            {
                return urlShortName;
            }
            else
            {
                _logger.LogError("Submission service configuration does not contain a short name for measure: " + measure);
            }

            return $"{measure.GetHashCode():X}";
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
                _logger.LogInformation(
                    $"Started consumer for topic '{nameof(KafkaTopic.SubmitReport)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {

                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            if (result == null)
                            {
                                consumer.Commit();
                                return;
                            }
                            string facilityId = string.Empty;
                            try
                            {
                                var key = result.Message.Key;
                                var value = result.Message.Value;
                                facilityId = key.FacilityId;

                                if (string.IsNullOrWhiteSpace(facilityId))
                                {
                                    throw new TransientException(
                                        $"{Name}: FacilityId is null or empty.");
                                }

                                if (value.PatientIds == null || value.PatientIds.Count == 0)
                                {
                                    throw new DeadLetterException(
                                        $"{Name}: PatientIds is null or contains no elements.");
                                }

                                if (value.MeasureIds == null || value.MeasureIds.Count == 0)
                                {
                                    throw new DeadLetterException(
                                        $"{Name}: MeasureIds is null or contains no elements.");
                                }

                                if (value.Organization == null)
                                {
                                    throw new DeadLetterException(
                                        $"{Name}: Organization is null.");
                                }

                                if (value.Aggregates == null || value.Aggregates.Count == 0)
                                {
                                    throw new DeadLetterException(
                                        $"{Name}: Aggregates is null or contains no elements.");
                                }

                                var httpClient = _httpClient.CreateClient();
                                string dtFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

                                #region Census Admitted Patient List
                                string censusRequestUrl = $"{_serviceRegistry.CensusServiceApiUrl}/Census/{key.FacilityId}/history/admitted?startDate={key.StartDate.ToString(dtFormat)}&endDate={key.EndDate.ToString(dtFormat)}";

                                //TODO: add method to get key that includes looking at redis for future use case
                                if (_linkTokenServiceConfig.Value.SigningKey is null)
                                    throw new Exception("Link Token Service Signing Key is missing.");

                                //Add link token
                                var token = _createSystemToken.ExecuteAsync(_linkTokenServiceConfig.Value.SigningKey, 5).Result;
                                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                                _logger.LogDebug("Requesting census from Census service: " + censusRequestUrl);
                                var censusResponse = await httpClient.GetAsync(censusRequestUrl, consumeCancellationToken);
                                var censusContent = await censusResponse.Content.ReadAsStringAsync(consumeCancellationToken);

                                if (!censusResponse.IsSuccessStatusCode)
                                    throw new TransientException("Response from Census service is not successful: " + censusContent);

                                List? admittedPatients;
                                try
                                {
                                    admittedPatients =
                                        System.Text.Json.JsonSerializer.Deserialize<Hl7.Fhir.Model.List>(
                                            censusContent,
                                            new JsonSerializerOptions().ForFhir());
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error deserializing admitted patients from Census service response.");
                                    _logger.LogDebug("Census service response: " + censusContent);
                                    throw new TransientException("Error deserializing admitted patients from Census service response: " + ex.Message + Environment.NewLine + ex.StackTrace, ex.InnerException);
                                }
                                #endregion

                                Bundle otherResourcesBundle = new Bundle();

                                string measureShortNames = value.MeasureIds
                                    .Select(GetMeasureShortName)
                                    .Aggregate((a, b) => $"{a}+{b}");

                                //Format: <nhsn-org-id>-<plus-separated-list-of-measure-ids>-<period-start>-<period-end?>-<timestamp>
                                //Per 2153, don't build with the trailing timestamp
                                dtFormat = "yyyyMMddTHHmmss";
                                string submissionDirectory = Path.Combine(_submissionConfig.SubmissionDirectory,
                                    $"{facilityId}-{measureShortNames}-{key.StartDate.ToString(dtFormat)}-{key.EndDate.ToString(dtFormat)}");

                                string fileName;
                                string contents;
                                var fhirSerializer = new FhirJsonSerializer();
                                try
                                {
                                    if (Directory.Exists(submissionDirectory))
                                    {
                                        Directory.Delete(submissionDirectory, true);
                                    }

                                    Directory.CreateDirectory(submissionDirectory);

                                    #region Device

                                    Hl7.Fhir.Model.Device device = new Device();
                                    device.DeviceName.Add(new Device.DeviceNameComponent()
                                    {
                                        Name = "Link"
                                    });

                                    Assembly assembly = Assembly.GetExecutingAssembly();
                                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                                    string? version = fvi?.FileVersion;

                                    (device.Version = new List<Device.VersionComponent>()).Add(new Device.VersionComponent
                                    {
                                        Value = version,
                                        ValueElement = new FhirString(version)
                                    });

                                    fileName = "sending-device.json";
                                    contents = await fhirSerializer.SerializeToStringAsync(device);

                                    await File.WriteAllTextAsync(submissionDirectory + "/" + fileName, contents,
                                        consumeCancellationToken);

                                    #endregion

                                    #region Organization

                                    fileName = "sending-organization.json";
                                    contents = await fhirSerializer.SerializeToStringAsync(value.Organization);

                                    await File.WriteAllTextAsync(submissionDirectory + "/" + fileName, contents,
                                        consumeCancellationToken);

                                    #endregion

                                    #region Patient List

                                    fileName = "patient-list.json";
                                    contents = await fhirSerializer.SerializeToStringAsync(admittedPatients);

                                    await File.WriteAllTextAsync(submissionDirectory + "/" + fileName, contents,
                                        consumeCancellationToken);

                                    #endregion

                                    #region Aggregates

                                    foreach (var aggregate in value.Aggregates)
                                    {
                                        string measureShortName = this.GetMeasureShortName(aggregate.Measure);
                                        fileName = $"aggregate-{measureShortName}.json";
                                        contents = await fhirSerializer.SerializeToStringAsync(aggregate);

                                        await File.WriteAllTextAsync(submissionDirectory + "/" + fileName, contents,
                                            consumeCancellationToken);
                                    }

                                    #endregion
                                }
                                catch (IOException ioException)
                                {
                                    throw new TransientException(ioException.Message,
                                        ioException.InnerException);
                                }

                                #region Patient and Other Resources Bundles

                                var patientIds = value.PatientIds.Select(p => p).ToList();

                                var batchSize = _submissionConfig.PatientBundleBatchSize;

                                while (patientIds.Any())
                                {
                                    var otherResourcesBag = new ConcurrentBag<Bundle>();

                                    List<string> batch = new List<string>();
                                    if (patientIds.Count > batchSize)
                                    {
                                        batch.AddRange(patientIds.Take(_submissionConfig.PatientBundleBatchSize).ToList());
                                    }
                                    else
                                    {
                                        batch.AddRange(patientIds);
                                    }

                                    var tasks = new List<Task>();

                                    foreach (var pid in batch)
                                    {
                                        tasks.Add(Task.Run(async () =>
                                        {
                                            var otherResources = await CreatePatientBundleFiles(submissionDirectory,
                                                pid,
                                                facilityId,
                                                key.ReportScheduleId, consumeCancellationToken);

                                            otherResourcesBag.Add(otherResources);
                                        }));
                                    }

                                    //Wait for our batch to be completed
                                    await Task.WhenAll(tasks);

                                    //move the OtherResources into the aggregate bundle
                                    foreach (var bundle in otherResourcesBag)
                                    {
                                        foreach (var resource in bundle.GetResources())
                                        {
                                            if (!otherResourcesBundle.GetResources().Any(r => r.Id == resource.Id))
                                            {
                                                otherResourcesBundle.AddResourceEntry(resource, GetFullUrl(resource));
                                            }
                                        }
                                    }

                                    //Remove these PatientIds from the list since they have been processed, before looping again
                                    batch.ForEach(p => patientIds.Remove(p));
                                }

                                fileName = "other-resources.json";
                                contents = await fhirSerializer.SerializeToStringAsync(otherResourcesBundle);

                                await File.WriteAllTextAsync(submissionDirectory + "/" + fileName, contents,
                                    consumeCancellationToken);

                                #endregion
                            }
                            catch (DeadLetterException ex)
                            {
                                _deadLetterExceptionHandler.HandleException(result, ex, facilityId);
                            }
                            catch (TransientException ex)
                            {
                                _transientExceptionHandler.HandleException(result, ex, facilityId);
                            }
                            catch (TimeoutException ex)
                            {
                                var transientException = new TransientException(ex.Message,  ex.InnerException);

                                _transientExceptionHandler.HandleException(result, transientException, facilityId);
                            }
                            catch (Exception ex)
                            {
                                _transientExceptionHandler.HandleException(result, ex, facilityId);
                            }
                            finally
                            {
                                consumer.Commit(result);
                            }
                        }, cancellationToken);

                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming message for topics: [{1}] at {2}", string.Join(", ", consumer.Subscription), DateTime.UtcNow);

                        if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                        {
                            throw new OperationCanceledException(ex.Error.Reason, ex);
                        }

                       string facilityId = GetFacilityIdFromHeader(ex.ConsumerRecord.Message.Headers);

                        _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);

                        var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                        consumer.Commit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error encountered in SubmitReportListener");
                        consumer.Commit();
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

        protected static string GetFacilityIdFromHeader(Headers headers)
        {
            string facilityId = string.Empty;

            if (headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionFacilityId, out var facilityIdBytes))
            {
                facilityId = Encoding.UTF8.GetString(facilityIdBytes);
            }

            return facilityId;
        }

        protected string GetRelativeReference(Resource resource)
        {
            return string.Format("{0}/{1}", resource.TypeName, resource.Id);
        }

        protected string GetFullUrl(Resource resource)
        {
            return string.Format(SubmissionConstants.BundlingFullUrlFormat, GetRelativeReference(resource));
        }
        /// <summary>
        /// Creates the Patient Bundle in the submission directory, and returns the 'OtherResources' bundle
        /// that will be aggregated and written as one file to the SubmissionDirectory.
        /// </summary>
        /// <param name="submissionDirectory"></param>
        /// <param name="patientId"></param>
        /// <param name="facilityId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<Bundle> CreatePatientBundleFiles(string submissionDirectory, string patientId, string facilityId, string reportScheduleId, CancellationToken cancellationToken)
        {
            var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

            var httpClient = _httpClient.CreateClient();

            //TODO: add method to get key that includes looking at redis for future use case
            if (_linkTokenServiceConfig.Value.SigningKey is null)
                throw new Exception("Link Token Service Signing Key is missing.");

            //Add link token
            var token = _createSystemToken.ExecuteAsync(_linkTokenServiceConfig.Value.SigningKey, 2).Result;
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            string requestUrl = $"{_serviceRegistry.ReportServiceApiUrl.Trim('/')}/Report/Bundle/Patient?FacilityId={facilityId}&PatientId={patientId}&reportScheduleId={reportScheduleId}";

            try
            {
                var response = await httpClient.GetAsync(requestUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(
                        $"Report Service Call unsuccessful: StatusCode: {response.StatusCode} | Response: {await response.Content.ReadAsStringAsync(cancellationToken)} | Query URL: {requestUrl}");
                }

                var patientSubmissionBundle = (PatientSubmissionModel?)await response.Content.ReadFromJsonAsync(typeof(PatientSubmissionModel), cancellationToken);

                if (patientSubmissionBundle == null || patientSubmissionBundle.PatientResources == null || patientSubmissionBundle.OtherResources == null)
                {
                    throw new Exception(
                        @$"One or More Required Objects are null: 
                                        patientSubmissionBundle: {patientSubmissionBundle == null}
                                        patientSubmissionBundle.PatientResources: {patientSubmissionBundle?.PatientResources == null}
                                        patientSubmissionBundle.OtherResources: {patientSubmissionBundle?.OtherResources == null}");
                }

                var otherResources = patientSubmissionBundle.OtherResources;

                string fileName = $"patient-{patientId}.json";
                string contents = await new FhirJsonSerializer().SerializeToStringAsync(patientSubmissionBundle.PatientResources);

                await File.WriteAllTextAsync(submissionDirectory + "/" + fileName, contents, cancellationToken);

                return otherResources;
            }
            catch (Exception ex)
            {
                throw new TransientException(ex.Message, ex.InnerException);
            }
        }
    }
}