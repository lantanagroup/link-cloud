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
        private readonly ITransientExceptionHandler<SubmitPayloadKey, SubmitPayloadValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<SubmitPayloadKey, SubmitPayloadValue> _deadLetterExceptionHandler;
        private readonly BlobStorageService _blobStorageService;
        private readonly PayloadSubmittedProducer _payloadSubmittedProducer;
        private readonly AuditableEventOccurredProducer _auditableEventOccurredProducer;
        private readonly ReportClient _reportClient;

        public SubmitPayloadListener(
            ILogger<SubmitPayloadListener> logger,
            IKafkaConsumerFactory<SubmitPayloadKey, SubmitPayloadValue> kafkaConsumerFactory,
            ITransientExceptionHandler<SubmitPayloadKey, SubmitPayloadValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<SubmitPayloadKey, SubmitPayloadValue> deadLetterExceptionHandler,
            BlobStorageService blobStorageService,
            PayloadSubmittedProducer payloadSubmittedProducer,
            AuditableEventOccurredProducer auditableEventOccurredProducer,
            ReportClient reportClient)
        {
            _logger = logger;

            _consumer = kafkaConsumerFactory.CreateConsumer(new()
            {
                GroupId = SubmissionConstants.ServiceName,
                EnableAutoCommit = false
            });

            _transientExceptionHandler = transientExceptionHandler;
            _transientExceptionHandler.ServiceName = SubmissionConstants.ServiceName;
            _transientExceptionHandler.Topic = TopicName + "-Retry";

            _deadLetterExceptionHandler = deadLetterExceptionHandler;
            _deadLetterExceptionHandler.ServiceName = SubmissionConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = TopicName + "-Error";

            _blobStorageService = blobStorageService;

            _payloadSubmittedProducer = payloadSubmittedProducer;

            _auditableEventOccurredProducer = auditableEventOccurredProducer;

            _reportClient = reportClient;
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
                    }
                }
                if (content == null)
                {
                    try
                    {
                        content = await GetPayloadViaRestAsync(message, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to retrieve payload via REST.");
                    }
                }
                if (content == null)
                {
                    throw new DeadLetterException("Failed to retrieve content for submission.");
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
                }
                if (uploaded)
                {
                    _payloadSubmittedProducer.Produce(
                        correlationId,
                        facilityId,
                        key?.ReportScheduleId!,
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

        private async Task<byte[]> GetPayloadViaRestAsync(Message<SubmitPayloadKey, SubmitPayloadValue> message, CancellationToken cancellationToken = default)
        {
            Bundle? bundle = message.Value.PayloadType switch
            {
                PayloadType.MeasureReportSubmissionEntry => await _reportClient.GetSubmissionBundleForPatientAsync(message.Key.FacilityId, message.Value.PatientId, message.Key.ReportScheduleId, cancellationToken),
                PayloadType.ReportSchedule => await _reportClient.GetManifestBundleAsync(message.Key.FacilityId, message.Key.ReportScheduleId, cancellationToken),
                _ => throw new ArgumentException($"Unexpected payload type: {message.Value.PayloadType}", nameof(message)),
            };
            if (bundle == null)
            {
                return null;
            }
            using MemoryStream stream = new();
            ReadOnlyMemory<byte> lineFeed = new([0x0a]);
            foreach (Bundle.EntryComponent entry in bundle.Entry)
            {
                await JsonSerializer.SerializeAsync(stream, entry.Resource, lenientJsonOptions);
                await stream.WriteAsync(lineFeed);
            }
            return stream.ToArray();
        }

        public override void Dispose()
        {
            base.Dispose();
            _consumer.Close();
            _consumer.Dispose();
        }
    }
}
