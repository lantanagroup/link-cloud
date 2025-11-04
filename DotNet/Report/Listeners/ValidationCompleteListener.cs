using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
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
        private readonly ITransientExceptionHandler<string, ValidationCompleteValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, ValidationCompleteValue> _deadLetterExceptionHandler;
        private readonly SubmitPayloadProducer _submitPayloadProducer;
        private readonly ReportManifestProducer _reportManifestProducer;
        private readonly BlobStorageService _blobStorageService;
        private readonly PatientReportSubmissionBundler _patientReportSubmissionBundler;
        private readonly AuditableEventOccurredProducer _auditableEventOccurredProducer;

        private string Name => this.GetType().Name;

        public ValidationCompleteListener(
            ILogger<ValidationCompleteListener> logger,
            IKafkaConsumerFactory<string, ValidationCompleteValue> kafkaConsumerFactory,
            ITransientExceptionHandler<string, ValidationCompleteValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<string, ValidationCompleteValue> deadLetterExceptionHandler,
            SubmitPayloadProducer submitPayloadProducer,
            IServiceScopeFactory serviceScopeFactory,
            BlobStorageService blobStorageService,
            PatientReportSubmissionBundler patientReportSubmissionBundler,
            ReportManifestProducer reportManifestProducer,
            AuditableEventOccurredProducer auditableEventOccurredProducer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _serviceScopeFactory = serviceScopeFactory;
            _submitPayloadProducer = submitPayloadProducer;
            _blobStorageService = blobStorageService;
            _patientReportSubmissionBundler = patientReportSubmissionBundler;
            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));
            _reportManifestProducer = reportManifestProducer;

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = nameof(KafkaTopic.ValidationComplete) + "-Retry";
            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ValidationComplete) + "-Error";
            _auditableEventOccurredProducer = auditableEventOccurredProducer;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async Task StartConsumerLoop(CancellationToken cancellationToken)
        {
            var consumerConfig = new ConsumerConfig()
            {
                GroupId = ReportConstants.ServiceName,
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
            var facilityId = result.Message.Key;
            var value = result.Message.Value;
            var reportId = value.ReportTrackingId;

            using var scope = _serviceScopeFactory.CreateScope();
            var measureReportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
            var submissionEntryManager = scope.ServiceProvider.GetRequiredService<ISubmissionEntryManager>();

            var schedule = await measureReportScheduledManager.SingleOrDefaultAsync(s => s.Id == reportId, cancellationToken);
            if (schedule == null)
            {
                throw new DeadLetterException($"No ReportSchedule found for ID {reportId}");
            }

            if (!result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
            {
                throw new DeadLetterException($"{Name}: Received message without correlation ID in topic: {result.Topic}, offset: {result.TopicPartitionOffset}");
            }

            var correlationIdStr = Encoding.UTF8.GetString(headerValue);


            var submissionEntries = await submissionEntryManager.FindAsync(
                e => e.ReportScheduleId == schedule.Id && e.PatientId == value.PatientId && e.Status == PatientSubmissionStatus.ValidationRequested, cancellationToken);

            if(!submissionEntries.Any() )
            {
                throw new DeadLetterException($"No Patient Submission Entries were found for schedule ID {schedule.Id}, patient ID {value.PatientId}, in status {PatientSubmissionStatus.ValidationRequested}");
            }

            foreach (var entry in submissionEntries)
            {
                if (!value.IsValid)
                {
                    var operationOutcome = new OperationOutcome();
                    var issue = new OperationOutcome.IssueComponent
                    {
                        Severity = OperationOutcome.IssueSeverity.Fatal,
                        Code = OperationOutcome.IssueType.Invalid,
                        Diagnostics = "Patient has failed Validation"
                    };
                    operationOutcome.Issue = new List<OperationOutcome.IssueComponent> { issue };
                    await submissionEntryManager.AddResourceAsync(entry, operationOutcome, ResourceCategoryType.Patient, cancellationToken);
                }

                entry.ValidationStatus = value.IsValid ? ValidationStatus.Passed : ValidationStatus.Failed;
                entry.Status = PatientSubmissionStatus.ValidationComplete;
                await submissionEntryManager.UpdateAsync(entry, cancellationToken);
            }

            if (!value.IsValid)
            {
                var patientSubmission = await _patientReportSubmissionBundler.GenerateBundle(facilityId, value.PatientId, schedule.Id);
                string? uri;
                try
                {
                    uri = (await _blobStorageService.UploadAsync(schedule, patientSubmission, cancellationToken))?.ToString();
                }
                catch (Exception ex)
                {
                    uri = null;
                    _logger.LogError(ex, "Failed to upload to blob storage.");
                    AuditEventMessage auditEvent = new()
                    {
                        FacilityId = facilityId,
                        CorrelationId = correlationIdStr,
                        EventDate = DateTime.UtcNow,
                        Notes = $"Failed to upload to blob storage: {ex.GetType().Name}: {ex.Message}"
                    };
                    await _auditableEventOccurredProducer.ProduceAsync(auditEvent, cancellationToken);
                }

                if (!string.IsNullOrEmpty(uri))
                {
                    foreach (var entry in submissionEntries)
                    {
                        entry.PayloadUri = uri;
                        await submissionEntryManager.UpdateAsync(entry, cancellationToken);
                    }
                }
            }

            try
            {
                await _submitPayloadProducer.Produce(schedule, PayloadType.MeasureReportSubmissionEntry, value.PatientId, correlationIdStr, submissionEntries.First().PayloadUri);
            }
            catch (ProduceException<SubmitPayloadKey, SubmitPayloadValue> ex)
            {
                _logger.LogError(ex, "An error was encountered generating a Submit Payload event.\n\tFacilityId: {facilityId}\n\t", schedule.FacilityId);
                throw new TransientException($"An error was encountered generating a Submit Payload event.\n\tFacilityId: {facilityId}\n\t", ex);
            }


            await _reportManifestProducer.Produce(schedule, correlationIdStr);
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