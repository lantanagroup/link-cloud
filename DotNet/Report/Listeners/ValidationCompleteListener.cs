using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Support;
using LantanaGroup.Link.Report.Application.Core;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
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
        private readonly ITransientExceptionHandler<string, ValidationCompleteValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, ValidationCompleteValue> _deadLetterExceptionHandler;
        private readonly SubmitPayloadProducer _submitPayloadProducer;

        private string Name => this.GetType().Name;

        public ValidationCompleteListener(
            ILogger<ValidationCompleteListener> logger,
            IKafkaConsumerFactory<string, ValidationCompleteValue> kafkaConsumerFactory,
            ITransientExceptionHandler<string, ValidationCompleteValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<string, ValidationCompleteValue> deadLetterExceptionHandler,
            SubmitPayloadProducer submitPayloadProducer,
            IServiceScopeFactory serviceScopeFactory,
            ServiceInformation serviceInformation,
            BlobStorageService blobStorageService)
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
                _logger.LogInformation("Started validation complete consumer for topic '{Topic}' at {StartTime}", nameof(KafkaTopic.ValidationComplete), DateTime.UtcNow);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var facilityId = string.Empty;
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            if (result == null)
                            {
                                consumer.Commit();
                                return;
                            }

                            facilityId = result.Message.Key;
                            try
                            {
                                await ProcessMessageAsync(result, consumeCancellationToken);
                                consumer.Commit(result);
                            }
                            catch (DeadLetterException ex)
                            {
                                _deadLetterExceptionHandler.HandleException(result, ex, facilityId);
                                consumer.Commit(result);
                            }
                            catch (TransientException ex)
                            {
                                _transientExceptionHandler.HandleException(result, ex, facilityId);
                                consumer.Commit(result);
                            }
                            catch (TimeoutException ex)
                            {
                                var exceptionMessage = $"Timeout exception encountered on {DateTime.UtcNow} for topics: [{string.Join(", ", consumer.Subscription)}] at offset: {result.TopicPartitionOffset}";
                                var transientException = new TransientException(exceptionMessage, ex);
                                _transientExceptionHandler.HandleException(result, transientException, facilityId);
                                consumer.Commit(result);
                            }
                            catch (Exception ex)
                            {
                                _transientExceptionHandler.HandleException(result, ex, facilityId);
                                consumer.Commit(result);
                            }
                        }, cancellationToken);
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming message for topics: [{Topics}] at {Timestamp}", string.Join(", ", consumer.Subscription), DateTime.UtcNow);

                        if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                        {
                            throw new OperationCanceledException(ex.Error.Reason, ex);
                        }

                        facilityId = GetFacilityIdFromHeader(ex.ConsumerRecord.Message.Headers);
                        _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);

                        var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                        consumer.Commit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error encountered in ValidationCompleteListener");
                        consumer.Commit();
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError(oce, "Operation Canceled: {Message}", oce.Message);
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
                throw new DeadLetterException($"{Name}: No scheduled report record was found (ReportId = {reportId}, FacilityId = {facilityId}).");
            }

            if (!result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
            {
                throw new DeadLetterException($"{Name}: Received message without correlation ID (ReportId = {reportId}, FacilityId = {facilityId}).");
            }

            _logger.LogDebug("Consuming ValidationComplete (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", facilityId, value.PatientId, reportId);

            var correlationIdStr = Encoding.UTF8.GetString(headerValue);
            var reportEntry = await reportEntryManager.GetEntry(schedule.Id, value.PatientId, cancellationToken);

            if (reportEntry == null)
            {
                throw new DeadLetterException($"{Name}: No patient report entry records were found (ReportId = {schedule.Id}, FacilityId = {facilityId}, PatientId {value.PatientId}).");
            }

            if (!value.IsValid)
            {
                var operationOutcome = new OperationOutcome()
                {
                    Id = Guid.NewGuid().ToString(),
                    Issue = new List<OperationOutcome.IssueComponent>() {
                        new OperationOutcome.IssueComponent() {
                            Severity = OperationOutcome.IssueSeverity.Error,
                            Code = OperationOutcome.IssueType.Invalid,
                            Diagnostics = "Patient has failed Validation"
                        }
                    }
                };

                var serializer = new FhirJsonSerializer();
                string json = serializer.SerializeToString(operationOutcome);

                try
                {
                    await patientAggregator.AppendResourceToBlob(reportEntry.AggregateReportBlobName, operationOutcome);
                }
                catch (Exception ex)
                {
                    throw new TransientException($"{Name}: Could not append OperationOutcome resource to patient aggregate report (ReportId = {schedule.Id}, FacilityId = {facilityId}, PatientId {value.PatientId}).");
                }
            }

            try
            {
                reportEntry.ReportingStatus = value.IsValid ? ReportingStatus.PassedValidation : ReportingStatus.FailedValidation;
                reportEntry.SubmissionStatus = SubmissionStatus.Submitting;
                await reportEntryManager.UpdateAsync(reportEntry, cancellationToken);

                await _submitPayloadProducer.Produce(schedule, PayloadType.MeasureReportSubmissionEntry, value.PatientId, correlationIdStr, reportEntry.AggregateReportUri);
            }
            catch (Exception ex)
            {
                throw new TransientException($"{Name}: An error was encountered when producing a Submit Payload event(ReportId = {schedule.Id}, FacilityId = {facilityId}, PatientId {value.PatientId}).", ex);
            }
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