using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Submission.Application.Services;
using LantanaGroup.Link.Submission.KafkaProducers;
using LantanaGroup.Link.Submission.Settings;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Submission.Listeners
{
    public class SubmitPayloadListener : BackgroundService
    {
        private const string TopicName = nameof(KafkaTopic.SubmitPayload);

        private static readonly JsonSerializerOptions lenientJsonOptions =
            new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector).UsingMode(DeserializerModes.Ostrich);

        private readonly ILogger<SubmitPayloadListener> _logger;
        private readonly IConsumer<SubmitPayloadKey, SubmitPayloadValue> _consumer;
        private readonly ITransientExceptionHandler<SubmitPayloadListener, SubmitPayloadKey, SubmitPayloadValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<SubmitPayloadListener, SubmitPayloadKey, SubmitPayloadValue> _deadLetterExceptionHandler;
        private readonly BlobStorageService _blobStorageService;
        private readonly PayloadSubmittedProducer _payloadSubmittedProducer;
        private readonly AuditableEventOccurredProducer _auditableEventOccurredProducer;

        public SubmitPayloadListener(
            ILogger<SubmitPayloadListener> logger,
            IKafkaConsumerFactory<SubmitPayloadKey, SubmitPayloadValue> kafkaConsumerFactory,
            ITransientExceptionHandler<SubmitPayloadListener, SubmitPayloadKey, SubmitPayloadValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<SubmitPayloadListener, SubmitPayloadKey, SubmitPayloadValue> deadLetterExceptionHandler,
            BlobStorageService blobStorageService,
            PayloadSubmittedProducer payloadSubmittedProducer,
            AuditableEventOccurredProducer auditableEventOccurredProducer)
        {
            _logger = logger;

            _consumer = kafkaConsumerFactory.CreateConsumer(new()
            {
                GroupId = SubmissionConstants.ServiceName,
                EnableAutoCommit = false
            });

            _transientExceptionHandler = transientExceptionHandler;
            _transientExceptionHandler.Topic = TopicName + "-Retry";

            _deadLetterExceptionHandler = deadLetterExceptionHandler;
            _deadLetterExceptionHandler.Topic = TopicName + "-Error";

            _blobStorageService = blobStorageService;

            _payloadSubmittedProducer = payloadSubmittedProducer;

            _auditableEventOccurredProducer = auditableEventOccurredProducer;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => ExecuteCoreAsync(stoppingToken), stoppingToken);
        }

        private async Task ExecuteCoreAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Subscribing: {}", TopicName);
            _consumer.Subscribe(TopicName);
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
                        _deadLetterExceptionHandler.HandleConsumeException(ex, null!);
                    }
                    TopicPartitionOffset? offset = result?.TopicPartitionOffset;
                    if (offset == null)
                    {
                        _consumer.Commit();
                    }
                    else
                    {
                        _consumer.Commit([offset]);
                    }
                }
            }
        }

        private async Task ConsumeAsync(
            ConsumeResult<SubmitPayloadKey, SubmitPayloadValue>? result,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Consumed: {}@{}", TopicName, result?.Offset.Value);
            if (result == null)
            {
                _logger.LogWarning("Consume result is null");
                _consumer.Commit();
                return;
            }
            Message<SubmitPayloadKey, SubmitPayloadValue>? message = result.Message;
            Headers? headers = message?.Headers;
            string? correlationId = headers == null ? null : KafkaHeaderHelper.GetCorrelationId(headers);
            SubmitPayloadKey? key = message?.Key;
            SubmitPayloadValue? value = message?.Value;
            if (value == null)
            {
                _logger.LogWarning("Message value is null");
                _consumer.Commit(result);
                return;
            }
            string? facilityId = key?.FacilityId;
            try
            {
                if (string.IsNullOrEmpty(facilityId))
                {
                    throw new DeadLetterException("Facility ID not specified.");
                }

                if (value.ReportTypes == null || value.ReportTypes.Count == 0)
                {
                    throw new DeadLetterException("Measure IDs not specified.");
                }

                byte[]? content = null;

                if (_blobStorageService.HasInternalClient())
                {
                    try
                    {
                        content = await _blobStorageService.DownloadFromInternalAsync(value, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to download from internal blob storage.");
                        await ProduceAuditEventAsync(facilityId, correlationId, $"Failed to download from internal blob storage: {ex}", cancellationToken);
                        
                        throw new TransientException("Failed to retrieve content for submission.");
                    }
                }

                bool uploaded = false;
                try
                {
                    await _blobStorageService.UploadToExternalAsync(key, value, content, cancellationToken);
                    uploaded = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload to external blob storage.");
                    await ProduceAuditEventAsync(facilityId, correlationId, $"Failed to upload to external blob storage: {ex}", cancellationToken);

                    throw new TransientException("Failed to submit content.");
                }
                if (uploaded)
                {
                    _payloadSubmittedProducer.Produce(
                        correlationId,
                        facilityId,
                        key.ReportScheduleId,
                        value.PayloadType,
                        value.PatientId);
                }
            }
            catch (TransientException ex)
            {
                _transientExceptionHandler.HandleException(result, ex, facilityId!);
            }
            catch (DeadLetterException ex)
            {
                _deadLetterExceptionHandler.HandleException(result, ex, facilityId!);
            }
            catch (Exception ex)
            {
                _deadLetterExceptionHandler.HandleException(result, ex, facilityId!);
            }
            finally
            {
                _consumer.Commit(result);
            }
        }

        private async Task ProduceAuditEventAsync(string facilityId, string? correlationId, string notes, CancellationToken cancellationToken = default)
        {
            AuditEventMessage auditEvent = new()
            {
                FacilityId = facilityId,
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
