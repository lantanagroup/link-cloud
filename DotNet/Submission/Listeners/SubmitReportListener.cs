using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Submission.Application.Models;
using MediatR;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Collections.Generic;

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
                GroupId = "SubmitReportEvent",
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

                            foreach (var patientId in value.PatientIds)
                            {
                                foreach (var reportId in value.PatientReportIds[patientId])
                                {
                                    string requestUrl = _submissionConfig.ReportServiceUrl + $"?reportId={reportId}";
                                    var response = await httpClient.GetAsync(requestUrl, cancellationToken);

                                    var measureReportSubmissionBundle =
                                        JsonConvert.DeserializeObject<MeasureReportSubmissionModel>(
                                            await response.Content.ReadAsStringAsync(cancellationToken));
                                }
                            }

                            #region File IO
                            string facilityDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _fileSystemConfig.FilePath.Trim('/'), key.FacilityId);
                            if (!Directory.Exists(facilityDirectory))
                            {
                                Directory.CreateDirectory(facilityDirectory);
                            }

                            var dtu = DateTime.UtcNow;
                            string fullFilePath = facilityDirectory + $"/submission_{value.MeasureReportScheduleId.Replace("-", "_")}.txt";

                            await File.WriteAllTextAsync(fullFilePath, measureReportSubmissionBundle.SubmissionBundle, cancellationToken);

                            if (!File.Exists(fullFilePath))
                            {
                                throw new TransientException($"{Name}: Bundle File Not Created", AuditEventType.Create);
                            }
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
    }
}
