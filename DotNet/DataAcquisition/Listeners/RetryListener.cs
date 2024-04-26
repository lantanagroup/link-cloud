
using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Settings;
using System.Text;
using static Confluent.Kafka.ConfigPropertyNames;

namespace LantanaGroup.Link.DataAcquisition.Listeners;

public class RetryListener : BackgroundService
{
    private readonly ILogger<RetryListener> _logger;
    private readonly IKafkaConsumerFactory<string, string> _kafkaConsumerFactory;
    private readonly IRetryEntityFactory _retryEntityFactory;
    private readonly IDeadLetterExceptionHandler<string, string> _deadLetterExceptionHandler;
    private readonly IRetryRepository _retryRepository;

    public RetryListener(ILogger<RetryListener> logger, IKafkaConsumerFactory<string, string> kafkaConsumerFactory, IRetryEntityFactory retryEntityFactory, IDeadLetterExceptionHandler<string, string> deadLetterExceptionHandler, IRetryRepository retryRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentNullException(nameof(kafkaConsumerFactory));
        _retryEntityFactory = retryEntityFactory ?? throw new ArgumentNullException(nameof(retryEntityFactory));
        _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentNullException(nameof(deadLetterExceptionHandler));
        _retryRepository = retryRepository ?? throw new ArgumentNullException(nameof(retryRepository));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
    }

    private async void StartConsumerLoop(CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig
        {
            GroupId = "DataAcquisitionRetry",
            EnableAutoCommit = false,
        };

        using var _consumer = _kafkaConsumerFactory.CreateConsumer(config);

        try
        {
            _consumer.Subscribe(new List<string> { KafkaTopic.DataAcquisitionRequestedRetry.ToString(), KafkaTopic.PatientCensusScheduledRetry.ToString() });

            _logger.LogInformation("Started Data Acquisition Service Retry consumer for topics: [{1}] {2}", string.Join(", ", _consumer.Subscription), DateTime.UtcNow);

            while (!cancellationToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? consumeResult;

                try
                {
                    await _consumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                    {
                        consumeResult = result;

                        if (consumeResult is not null)
                        {
                            var retryEntity = _retryEntityFactory.CreateRetryEntity(consumeResult.Message.Value, consumeResult.Topic);
                            await _retryRepository.AddAsync(retryEntity);
                        }
                    }, cancellationToken);
                }
                catch (ConsumeException ex)
                {
                    if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        throw new OperationCanceledException(ex.Error.Reason, ex);
                    }

                    var facilityId = GetFacilityIdFromHeader(ex.ConsumerRecord.Message.Headers);
                    var exceptionConsumerResult = new ConsumeResult<string, string>()
                    {
                        Message = new Message<string, string>()
                        {
                            Headers = ex.ConsumerRecord.Message.Headers,
                            Key = ex.ConsumerRecord != null && ex.ConsumerRecord.Message != null && ex.ConsumerRecord.Message.Key != null ? Encoding.UTF8.GetString(ex.ConsumerRecord.Message.Key) : string.Empty,
                            Value = ex.ConsumerRecord != null && ex.ConsumerRecord.Message != null && ex.ConsumerRecord.Message.Value != null ? Encoding.UTF8.GetString(ex.ConsumerRecord.Message.Value) : string.Empty,
                        },
                    };

                    _deadLetterExceptionHandler.Topic = ex.ConsumerRecord.Topic.Replace("-Retry", "-Error");
                    _deadLetterExceptionHandler.HandleException(exceptionConsumerResult, ex, AuditEventType.Create, facilityId);
                    _logger.LogError(ex, "Error consuming message for topics: [{1}] at {2}", string.Join(", ", _consumer.Subscription), DateTime.UtcNow );
                    continue;
                }
                catch (OperationCanceledException oce)
                {
                    _logger.LogError(oce, "Operation Cancelled: {1}", oce.Message);
                    _consumer.Close();
                    _consumer.Dispose();
                }
            }
        }
        catch (OperationCanceledException oce)
        {
            _logger.LogError(oce, "Operation Cancelled: {1}", oce.Message);
            _consumer.Close();
            _consumer.Dispose();
        }
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
