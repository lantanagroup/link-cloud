using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Submission.Application.Config;
using LantanaGroup.Link.Submission.Application.Interfaces;
using LantanaGroup.Link.Submission.KafkaProducers;
using LantanaGroup.Link.Submission.Settings;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Submission.Listeners
{
    public class SubmitPayloadListener : BackgroundService
    {
        private const string TopicName = nameof(KafkaTopic.SubmitPayload);

        private static readonly JsonSerializerOptions lenientJsonOptions =
            new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector).UsingMode(DeserializationMode.Ostrich);

        private readonly ILogger<SubmitPayloadListener> _logger;
        private readonly KafkaAssignmentTracker _assignmentTracker = new();
        private readonly IConsumer<string, SubmitPayloadValue> _consumer;
        private readonly ITransientExceptionHandler<SubmitPayloadListener, string, SubmitPayloadValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<SubmitPayloadListener, string, SubmitPayloadValue> _deadLetterExceptionHandler;
        private readonly IStorageService _blobStorageService;
        private readonly ISubmissionServiceMetrics _metrics;
        private readonly PayloadSubmittedProducer _payloadSubmittedProducer;
        private readonly AuditableEventOccurredProducer _auditableEventOccurredProducer;
        private readonly ExternalBlobStorageSettings _externalBlobStorageSettings;

        public SubmitPayloadListener(
            ILogger<SubmitPayloadListener> logger,
            IKafkaConsumerFactory<string, SubmitPayloadValue> kafkaConsumerFactory,
            ITransientExceptionHandler<SubmitPayloadListener, string, SubmitPayloadValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<SubmitPayloadListener, string, SubmitPayloadValue> deadLetterExceptionHandler,
            IStorageService blobStorageService,
            ISubmissionServiceMetrics metrics,
            PayloadSubmittedProducer payloadSubmittedProducer,
            AuditableEventOccurredProducer auditableEventOccurredProducer,
            IOptions<ExternalBlobStorageSettings> externalBlobStorageSettings)
        {
            _logger = logger;

            _consumer = kafkaConsumerFactory.CreateConsumer(new()
            {
                GroupId = SubmissionConstants.ServiceName,
                EnableAutoCommit = false
            }, assignmentTracker: _assignmentTracker);

            _transientExceptionHandler = transientExceptionHandler;
            _transientExceptionHandler.Topic = TopicName + "-Retry";

            _deadLetterExceptionHandler = deadLetterExceptionHandler;
            _deadLetterExceptionHandler.Topic = TopicName + "-Error";

            _blobStorageService = blobStorageService;

            _metrics = metrics;

            _payloadSubmittedProducer = payloadSubmittedProducer;

            _auditableEventOccurredProducer = auditableEventOccurredProducer;
            _externalBlobStorageSettings = externalBlobStorageSettings.Value;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => ExecuteCoreAsync(stoppingToken), stoppingToken);
        }

        private async Task ExecuteCoreAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Subscribing: {}", TopicName);
            _consumer.Subscribe(KafkaTopicNames.Subscription(TopicName, SubmissionConstants.ServiceName));
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _consumer.ConsumeWithInstrumentation(ConsumeAsync, cancellationToken);
                }
                catch (ConsumeException ex)
                {
                    if (ex.Error?.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        throw;
                    }
                    ConsumeResult<byte[], byte[]>? result = ex.ConsumerRecord;
                    if (result != null)
                    {
                        var rawKey = result.Message?.Key != null ? Encoding.UTF8.GetString(result.Message.Key) : null;
                        var facilityId = KafkaIdentity.Facility(null, rawKey);
                        _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId ?? string.Empty);
                        _consumer.SafeCommit(new List<TopicPartitionOffset> { result.TopicPartitionOffset }, _logger);
                    }
                }
            }
        }

        private async Task ConsumeAsync(
            ConsumeResult<string, SubmitPayloadValue>? result,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Consumed: {}@{}", TopicName, result?.Offset.Value);
            if (result == null)
            {
                _logger.LogWarning("Consume result is null");
                _consumer.SafeCommit(_logger);
                return;
            }

            var accounted = false;
            string? facilityId = null;
            try
            {
                Message<string, SubmitPayloadValue>? message = result.Message;
                Headers? headers = message?.Headers;
                string? correlationId = headers == null ? null : KafkaHeaderHelper.GetCorrelationId(headers);
                SubmitPayloadValue? value = message?.Value;

                if (value == null)
                {
                    _logger.LogWarning("Message value is null");
                    accounted = true;
                    return;
                }

                facilityId = KafkaIdentity.Facility(value.FacilityId, message?.Key);
                var patientId = KafkaIdentity.Patient(value.PatientId, message?.Key);
                var reportScheduleId = KafkaIdentity.ReportSchedule(value.ReportScheduleId, message?.Key);

                if (string.IsNullOrEmpty(facilityId))
                {
                    throw new DeadLetterException("Facility ID not specified.");
                }

                if (reportScheduleId is null)
                {
                    throw new DeadLetterException("Report schedule id not specified.");
                }

                if (value.ReportTypes == null || value.ReportTypes.Count == 0)
                {
                    throw new DeadLetterException("Measure IDs not specified.");
                }

                var payloadKey = new SubmitPayloadKey
                {
                    FacilityId = facilityId,
                    ReportScheduleId = reportScheduleId.Value
                };

                if (_externalBlobStorageSettings.SuppressManifest && value.PayloadType == PayloadType.ReportSchedule)
                {
                    _logger.LogInformation(
                        "Skipping external manifest upload for ReportScheduleId={ReportScheduleId}, FacilityId={FacilityId} because ExternalBlobStorage:SuppressManifest=true.",
                        reportScheduleId,
                        facilityId);

                    _payloadSubmittedProducer.Produce(
                        correlationId,
                        facilityId,
                        reportScheduleId.Value,
                        value.PayloadType,
                        patientId);
                    accounted = true;
                    return;
                }

                byte[]? content = null;

                if (_blobStorageService.HasInternalClient())
                {
                    try
                    {
                        content = await _blobStorageService.DownloadFromInternalAsync(value, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to download from internal blob storage.");
                        await ProduceAuditEventAsync(facilityId, patientId, correlationId, $"Failed to download from internal blob storage: {ex}", cancellationToken);

                        throw new TransientException("Failed to download from internal blob storage.");
                    }
                }

                bool uploaded = false;
                try
                {
                    List<KeyValuePair<string, object?>> metricTags = _metrics.BuildTags(correlationId, reportScheduleId.Value, patientId, facilityId, _blobStorageService.DestinationType);
                    Stopwatch uploadStopwatch = Stopwatch.StartNew();

                    await _blobStorageService.UploadToExternalAsync(payloadKey, value, content, cancellationToken);

                    uploadStopwatch.Stop();
                    _metrics.IncrementResourceCount(metricTags);
                    _metrics.IncrementUploadCount(metricTags, "success");
                    _metrics.RecordUploadDuration(uploadStopwatch.Elapsed.TotalMilliseconds, metricTags);
                    _metrics.RecordUploadSize(content.LongLength, metricTags);
                    uploaded = true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload to external blob storage.");
                    await ProduceAuditEventAsync(facilityId, patientId, correlationId, $"Failed to upload to external blob storage: {ex}", cancellationToken);
                    List<KeyValuePair<string, object?>> failureTags = _metrics.BuildTags(correlationId, reportScheduleId.Value, patientId, facilityId, _blobStorageService.DestinationType);
                    _metrics.IncrementUploadCount(failureTags, "failure");

                    throw new TransientException("Failed to upload to external blob storage.");
                }

                if (uploaded)
                {
                    _payloadSubmittedProducer.Produce(
                        correlationId,
                        facilityId,
                        reportScheduleId.Value,
                        value.PayloadType,
                        patientId);
                }

                accounted = true;
            }
            catch (TransientException ex)
            {
                _transientExceptionHandler.HandleException(result, ex, facilityId ?? string.Empty);
                accounted = true;
            }
            catch (DeadLetterException ex)
            {
                _deadLetterExceptionHandler.HandleException(result, ex, facilityId ?? string.Empty);
                accounted = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _deadLetterExceptionHandler.HandleException(result, ex, facilityId ?? string.Empty);
                accounted = true;
            }
            finally
            {
                if (accounted && !cancellationToken.IsCancellationRequested)
                {
                    _assignmentTracker.MarkProcessed(result);
                    _consumer.SafeCommit(result, _logger);
                }
            }
        }

        private async Task ProduceAuditEventAsync(string facilityId, string? patientId, string? correlationId, string notes, CancellationToken cancellationToken = default)
        {
            AuditEventMessage auditEvent = new()
            {
                FacilityId = facilityId,
                PatientId = patientId,
                CorrelationId = correlationId,
                EventDate = DateTime.UtcNow,
                Notes = notes
            };
            try
            {
                await _auditableEventOccurredProducer.ProduceAsync(auditEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to produce audit event.");
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _consumer.Close();
            _consumer.Dispose();
        }
    }
}
