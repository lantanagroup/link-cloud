using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Support;
using LantanaGroup.Link.Report.Application.Core;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using ReportingStatus = LantanaGroup.Link.Report.Domain.Enums.ReportingStatus;
using SubmissionStatus = LantanaGroup.Link.Report.Domain.Enums.SubmissionStatus;
using LantanaGroup.Link.Shared.Settings;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Listeners
{
    public class ValidationCompleteListener : BackgroundService
    {
        private readonly ILogger<ValidationCompleteListener> _logger;
        private readonly IKafkaConsumerFactory<string, ValidationCompleteValue> _kafkaConsumerFactory;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ServiceInformation _serviceInformation;
        private readonly ITransientExceptionHandler<ValidationCompleteListener, string, ValidationCompleteValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<ValidationCompleteListener, string, ValidationCompleteValue> _deadLetterExceptionHandler;
        private readonly SubmitPayloadProducer _submitPayloadProducer;

        private readonly IExceptionLogger<ValidationCompleteListener> _exceptionLogger;

        public ValidationCompleteListener(
            ILogger<ValidationCompleteListener> logger,
            IKafkaConsumerFactory<string, ValidationCompleteValue> kafkaConsumerFactory,
            ITransientExceptionHandler<ValidationCompleteListener, string, ValidationCompleteValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<ValidationCompleteListener, string, ValidationCompleteValue> deadLetterExceptionHandler,
            SubmitPayloadProducer submitPayloadProducer,
            IServiceScopeFactory serviceScopeFactory,
            ServiceInformation serviceInformation,
            BlobStorageService blobStorageService,
            IExceptionLogger<ValidationCompleteListener> exceptionLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _serviceScopeFactory = serviceScopeFactory;
            _serviceInformation = serviceInformation;
            _submitPayloadProducer = submitPayloadProducer;
            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.Topic = nameof(KafkaTopic.ValidationComplete) + "-Retry";
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ValidationComplete) + "-Error";
            _exceptionLogger = exceptionLogger ?? throw new ArgumentNullException(nameof(exceptionLogger));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async Task StartConsumerLoop(CancellationToken cancellationToken)
        {
            var consumerConfig = new ConsumerConfig()
            {
                GroupId = _serviceInformation.ServiceConfigName,
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.ValidationComplete));
                _logger.LogInformation("{Name}: Started validation complete consumer for topic '{Topic}' at {StartTime}", nameof(ValidationCompleteListener), nameof(KafkaTopic.ValidationComplete), DateTime.UtcNow);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var facilityId = string.Empty;
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            if (result == null)
                            {
                                throw new DeadLetterException($"Received null message from topic '{nameof(KafkaTopic.ValidationComplete)}'.");
                            }

                            facilityId = result.Message.Key;
                            try
                            {
                                await ProcessMessageAsync(result, consumeCancellationToken);
                                consumer.SafeCommit(result, _logger);
                            }
                            catch (DeadLetterException ex)
                            {
                                _deadLetterExceptionHandler.HandleException(result, ex, facilityId);
                                consumer.SafeCommit(result, _logger);
                            }
                            catch (TransientException ex)
                            {
                                _transientExceptionHandler.HandleException(result, ex, facilityId);
                                consumer.SafeCommit(result, _logger);
                            }
                            catch (TimeoutException ex)
                            {
                                var exceptionMessage = $"Timeout exception encountered on {DateTime.UtcNow} for topics: [{string.Join(", ", consumer.Subscription)}] at offset: {result.TopicPartitionOffset}";
                                var transientException = new TransientException(exceptionMessage, ex);
                                _transientExceptionHandler.HandleException(result, transientException, facilityId);
                                consumer.SafeCommit(result, _logger);
                            }
                            catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                _transientExceptionHandler.HandleException(result, ex, facilityId);
                                consumer.SafeCommit(result, _logger);
                            }
                        }, cancellationToken);
                    }
                    catch (ConsumeException ex)
                    {
                        if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                        {
                            throw new OperationCanceledException(ex.Error.Reason, ex);
                        }

                        facilityId = GetFacilityIdFromHeader(ex.ConsumerRecord.Message.Headers);
                        _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);

                        var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                        consumer.SafeCommit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset }, _logger);
                    }
                    catch (Exception ex)
                    {
                        _exceptionLogger.Handle(ex, "Error encountered in ValidationCompleteListener", LogLevel.Error);
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _exceptionLogger.Handle(oce, "Operation Canceled", LogLevel.Error);
                consumer.Close();
                consumer.Dispose();
            }
        }

        public async Task ProcessMessageAsync(ConsumeResult<string, ValidationCompleteValue> result, CancellationToken cancellationToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
            var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();
            var patientAggregator = scope.ServiceProvider.GetRequiredService<PatientAggregator>();

            var facilityId = result.Message.Key;
            var value = result.Message.Value;
            var reportId = Guid.Parse(value.ReportTrackingId);

            var schedule = await reportScheduledManager.SingleOrDefaultAsync(s => s.Id == reportId, cancellationToken);

            if (schedule == null)
            {
                throw new DeadLetterException($"No scheduled report record was found (ReportId = {reportId}, FacilityId = {facilityId}).");
            }

            if (!result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
            {
                throw new DeadLetterException($"Received message without correlation ID (ReportId = {reportId}, FacilityId = {facilityId}).");
            }

            _logger.LogDebug("Consuming ValidationComplete (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", facilityId, value.PatientId, reportId);

            var correlationIdStr = Encoding.UTF8.GetString(headerValue);
            var reportEntry = await reportEntryManager.GetEntry(schedule.Id, value.PatientId, cancellationToken);

            if (reportEntry == null)
            {
                throw new DeadLetterException($"No patient report entry records were found (ReportId = {schedule.Id}, FacilityId = {facilityId})");
            }

            if (!value.IsValid)
            {
                var operationOutcome = GetOperationOutcome();

                var serializer = new FhirJsonSerializer();
                string json = serializer.SerializeToString(operationOutcome);

                await patientAggregator.AppendResourceToBlob(reportEntry.AggregateReportBlobName, operationOutcome);
            }

            reportEntry.ReportingStatus = value.IsValid ? ReportingStatus.PassedValidation : ReportingStatus.FailedValidation;
            reportEntry.SubmissionStatus = SubmissionStatus.Submitting;

            await reportEntryManager.UpdateAsync(reportEntry, cancellationToken);

            await _submitPayloadProducer.Produce(schedule, PayloadType.MeasureReportSubmissionEntry, value.PatientId, correlationIdStr, reportEntry.AggregateReportUri);
        }

        private static OperationOutcome GetOperationOutcome()
        {
            OperationOutcome operationOutcome = new()
            {
                Id = Guid.NewGuid().ToString()
            };

            operationOutcome.AddIssue(new OperationOutcome.IssueComponent
            {
                Severity = OperationOutcome.IssueSeverity.Error,
                Code = OperationOutcome.IssueType.Invalid,
                Diagnostics = "Patient has failed Validation"
            });

            return operationOutcome;
        }

        private static string GetFacilityIdFromHeader(Headers headers)
        {
            string facilityId = string.Empty;
            if (headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionFacilityId, out var facilityIdBytes))
            {
                facilityId = Encoding.UTF8.GetString(facilityIdBytes);
            }
            return facilityId;
        }
    }
}