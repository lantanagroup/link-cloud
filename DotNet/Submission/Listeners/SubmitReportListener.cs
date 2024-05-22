﻿using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Submission.Application.Models;
using LantanaGroup.Link.Submission.Settings;
using MediatR;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text.Json;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;
using Task = System.Threading.Tasks.Task;

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

        public SubmitReportListener(ILogger<SubmitReportListener> logger,
            IKafkaConsumerFactory<SubmitReportKey, SubmitReportValue> kafkaConsumerFactory,
            IMediator mediator, IOptions<SubmissionServiceConfig> submissionConfig,
            IOptions<FileSystemConfig> fileSystemConfig, IHttpClientFactory httpClient,
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
                _logger.LogInformation(
                    $"Started consumer for topic '{nameof(KafkaTopic.SubmitReport)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var consumeResult = new ConsumeResult<SubmitReportKey, SubmitReportValue>();
                    string facilityId = string.Empty;
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                        {
                            consumeResult = result;

                            try
                            {
                                if (consumeResult == null)
                                {
                                    throw new DeadLetterException($"{Name}: consumeResult is null", AuditEventType.Create);
                                }

                                var key = consumeResult.Message.Key;
                                var value = consumeResult.Message.Value;
                                facilityId = key.FacilityId;

                                if (string.IsNullOrWhiteSpace(key.FacilityId))
                                {
                                    throw new DeadLetterException(
                                        $"{Name}: FacilityId is null or empty.", AuditEventType.Create);
                                }

                                var httpClient = _httpClient.CreateClient();
                                string dtFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
                                string censusRequestUrl = _submissionConfig.CensusUrl +
                                                          $"/{key.FacilityId}/history/admitted?startDate={key.StartDate.ToString(dtFormat)}&endDate={key.EndDate.ToString(dtFormat)}";
                                
                                _logger.LogDebug("Requesting census from Census service: " + censusRequestUrl);
                                var censusResponse = await httpClient.GetAsync(censusRequestUrl, cancellationToken);
                                var censusContent = await censusResponse.Content.ReadAsStringAsync(cancellationToken);
                                List? admittedPatients = null;

                                if (!censusResponse.IsSuccessStatusCode)
                                    throw new TransientException("Response from Census service is not successful: " + censusContent, AuditEventType.Query);

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
                                    throw;
                                }

                                string dataAcqRequestUrl =
                                    _submissionConfig.DataAcquisitionUrl + $"/{key.FacilityId}/QueryPlans";
                                var dataAcqResponse = await httpClient.GetAsync(dataAcqRequestUrl, cancellationToken);
                                var queryPlans = await dataAcqResponse.Content.ReadAsStringAsync(cancellationToken);

                                Bundle otherResourcesBundle = new Bundle();

                                //Format: <nhsn-org-id>-<plus-separated-list-of-measure-ids>-<period-start>-<period-end?>-<timestamp>
                                //Per 2153, don't build with the trailing timestamp
                                string submissionDirectory = Path.Combine(_submissionConfig.SubmissionDirectory,
                                    $"{facilityId}-{value.MeasureIds}-{key.StartDate.ToShortDateString()}-{key.EndDate.ToShortDateString()}");
                                if (Directory.Exists(submissionDirectory))
                                {
                                    Directory.Delete(submissionDirectory, true);
                                }

                                Directory.CreateDirectory(submissionDirectory);

                                var options = new JsonSerializerOptions().ForFhir();

                                #region Device

                                Hl7.Fhir.Model.Device device = new Device();
                                device.DeviceName.Add(new Device.DeviceNameComponent()
                                    {
                                        Name = "Link"
                                    }
                                );

                                string fileName = "sending-device.json";
                                string contents = System.Text.Json.JsonSerializer.Serialize(device, options);

                                await File.WriteAllTextAsync(submissionDirectory + "/" + fileName, contents,
                                    cancellationToken);

                                #endregion

                                #region Organization
                                fileName = "sending-organization.json";
                                contents = System.Text.Json.JsonSerializer.Serialize(value.Organization, options);

                                await File.WriteAllTextAsync(submissionDirectory + "/" + fileName, contents,
                                    cancellationToken);

                                #endregion

                                #region Patient List

                                fileName = "patient-list.json";
                                contents = System.Text.Json.JsonSerializer.Serialize(admittedPatients, options);

                                await File.WriteAllTextAsync(submissionDirectory + "/" + fileName, contents,
                                    cancellationToken);
                                #endregion


                                #region Query Plans

                                fileName = "query-plan.yml";
                                contents = queryPlans;

                                await File.WriteAllTextAsync(submissionDirectory + "/" + fileName, contents,
                                    cancellationToken);

                                #endregion

                                #region Aggregates

                                foreach (var aggregate in value.Aggregates)
                                {
                                    var agg = System.Text.Json.JsonSerializer.Deserialize<MeasureReport>(aggregate, options);
                                    fileName = $"aggregate-{agg.Measure}.json";
                                    contents = aggregate;

                                    await File.WriteAllTextAsync(submissionDirectory + "/" + fileName, contents,
                                        cancellationToken);
                                }

                                value.Aggregates.Clear();

                                #endregion

                                #region Patient and Other Resources Bundles

                                var patientIds = value.PatientIds.Distinct().ToList();
                                value.PatientIds.Clear();

                                var batchSize = _submissionConfig.PatientBundleBatchSize;

                                while (patientIds.Any())
                                {
                                    var otherResourcesBag = new SynchronizedCollection<Bundle>();

                                    List<string> batch;
                                    if (patientIds.Count > batchSize)
                                    {
                                        batch = patientIds.Take(_submissionConfig.PatientBundleBatchSize).ToList();
                                    }
                                    else
                                    {
                                        batch = patientIds;
                                    }

                                    var tasks = new List<Task>();

                                    foreach (var pid in batch)
                                    {
                                        tasks.Add(Task.Run(async () =>
                                        {
                                            var otherResources = await CreatePatientBundleFiles(submissionDirectory,
                                                pid,
                                                facilityId,
                                                key.StartDate, key.EndDate, cancellationToken);

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
                                            if (otherResourcesBundle.GetResources().All(r => r.Id != resource.Id))
                                            {
                                                otherResourcesBundle.AddResourceEntry(resource, GetFullUrl(resource));
                                            }
                                        }
                                    }

                                    //Remove these PatientIds from the list since they have been processed, before looping again
                                    batch.ForEach(p => patientIds.Remove(p));
                                }

                                fileName = "other-resources.json";
                                contents = System.Text.Json.JsonSerializer.Serialize(otherResourcesBundle, options);

                                await File.WriteAllTextAsync(submissionDirectory + "/" + fileName, contents,
                                    cancellationToken);

                                #endregion
                            }
                            catch (DeadLetterException ex)
                            {
                                _deadLetterExceptionHandler.HandleException(consumeResult, ex, facilityId);
                            }
                            catch (TransientException ex)
                            {
                                _transientExceptionHandler.HandleException(consumeResult, ex, facilityId);
                            }
                            catch (TimeoutException ex)
                            {
                                var transientException = new TransientException(ex.Message, AuditEventType.Submit, ex.InnerException);

                                _transientExceptionHandler.HandleException(consumeResult, transientException, facilityId);
                            }
                            catch (Exception ex)
                            {
                                _deadLetterExceptionHandler.HandleException(ex, facilityId, AuditEventType.Create);
                            }
                            finally
                            {
                                consumer.Commit(consumeResult);
                            }
                        }, cancellationToken);

                    }
                    catch (ConsumeException ex)
                    {
                        if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                        {
                            throw new OperationCanceledException(ex.Error.Reason, ex);
                        }

                        _deadLetterExceptionHandler.HandleException(new DeadLetterException($"{Name}: " + ex.Message, AuditEventType.Create, ex.InnerException), facilityId);
                        consumer.Commit();
                    }
                    catch (Exception ex)
                    {
                        _deadLetterExceptionHandler.HandleException(ex, facilityId, AuditEventType.Create);
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
        private async Task<Bundle> CreatePatientBundleFiles(string submissionDirectory, string patientId, string facilityId, DateTime startDate,
            DateTime endDate, CancellationToken cancellationToken)
        {
            var options = new JsonSerializerOptions().ForFhir();
            var httpClient = _httpClient.CreateClient();

            string requestUrl = _submissionConfig.ReportServiceUrl.Trim('/') +
                                $"/Bundle/Patient?FacilityId={facilityId}&PatientId={patientId}&StartDate={startDate}&EndDate={endDate}";
            var response = await httpClient.GetAsync(requestUrl, cancellationToken);

            var patientSubmissionBundle =
                JsonConvert.DeserializeObject<MeasureReportSubmissionModel>(
                    await response.Content.ReadAsStringAsync(cancellationToken));

            var patientBundle = System.Text.Json.JsonSerializer.Deserialize<Bundle>(patientSubmissionBundle.PatientResources, options);

            var otherResources = System.Text.Json.JsonSerializer.Deserialize<Bundle>(patientSubmissionBundle.OtherResources, options);

            string fileName = $"patient-{patientId}.json";
            string contents = System.Text.Json.JsonSerializer.Serialize(patientBundle, options);

            await File.WriteAllTextAsync(submissionDirectory + "/" + fileName, contents, cancellationToken);

            return otherResources;
        }
    }
}