using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Submission.Application.Services;
using LantanaGroup.Link.Submission.KafkaProducers;
using LantanaGroup.Link.Submission.Settings;

namespace LantanaGroup.Link.Submission.Listeners
{
    public class SubmitPayloadListener : BackgroundService
    {
        private const string TopicName = nameof(KafkaTopic.SubmitPayload);

        private readonly ILogger<SubmitPayloadListener> _logger;
        private readonly IConsumer<SubmitPayloadKey, SubmitPayloadValue> _consumer;
        private readonly ITransientExceptionHandler<SubmitPayloadKey, SubmitPayloadValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<SubmitPayloadKey, SubmitPayloadValue> _deadLetterExceptionHandler;
        private readonly BlobStorageService _blobStorageService;
        private readonly PayloadSubmittedProducer _payloadSubmittedProducer;

        public SubmitPayloadListener(
            ILogger<SubmitPayloadListener> logger,
            IKafkaConsumerFactory<SubmitPayloadKey, SubmitPayloadValue> kafkaConsumerFactory,
            ITransientExceptionHandler<SubmitPayloadKey, SubmitPayloadValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<SubmitPayloadKey, SubmitPayloadValue> deadLetterExceptionHandler,
            BlobStorageService blobStorageService,
            PayloadSubmittedProducer payloadSubmittedProducer)
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
                if (string.IsNullOrEmpty(value.PayloadUri))
                {
                    throw new DeadLetterException("Payload URI not specified.");
                }
                if (value.MeasureIds == null || value.MeasureIds.Count == 0)
                {
                    throw new DeadLetterException("Measure IDs not specified.");
                }
                byte[] content;
                try
                {
                    content = await _blobStorageService.DownloadFromInternalAsync(value, cancellationToken);
                }
                catch (Exception ex)
                {
                    // TODO: Fall back to REST call to Report (but how?)
                    throw new TransientException("Failed to download.", ex);
                }
                try
                {
                    await _blobStorageService.UploadToExternalAsync(value, content, cancellationToken);
                }
                catch (Exception ex)
                {
                    // TODO: Fall back to local filesystem submission (but how?)
                    throw new TransientException("Failed to upload.", ex);
                }
                string? correlationId = headers == null ? null : KafkaHeaderHelper.GetCorrelationId(headers);
                _payloadSubmittedProducer.Produce(
                    correlationId,
                    facilityId,
                    key?.ReportScheduleId!,
                    value.PayloadType,
                    value.PatientId);
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

        public override void Dispose()
        {
            base.Dispose();
            _consumer.Close();
            _consumer.Dispose();
        }
    }
}
