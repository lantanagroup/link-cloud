﻿
using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.Options;
using Quartz;
using System.Text;

namespace LantanaGroup.Link.DataAcquisition.Listeners;

public class RetryListener : BackgroundService
{
    private readonly ILogger<RetryListener> _logger;
    private readonly IKafkaConsumerFactory<string, string> _kafkaConsumerFactory;
    private readonly IRetryEntityFactory _retryEntityFactory;
    private readonly IDeadLetterExceptionHandler<string, string> _deadLetterExceptionHandler;
    private readonly IOptions<ConsumerSettings> _consumerSettings;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public RetryListener(
        ILogger<RetryListener> logger, 
        IKafkaConsumerFactory<string, string> kafkaConsumerFactory, 
        IRetryEntityFactory retryEntityFactory, 
        IDeadLetterExceptionHandler<string, string> deadLetterExceptionHandler, 
        IOptions<ConsumerSettings> consumerSettings, 
        ISchedulerFactory schedulerFactory,
        IServiceScopeFactory serviceScopeFactory
        )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentNullException(nameof(kafkaConsumerFactory));
        _retryEntityFactory = retryEntityFactory ?? throw new ArgumentNullException(nameof(retryEntityFactory));
        _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentNullException(nameof(deadLetterExceptionHandler));
        _consumerSettings = consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings));
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
    }

    private async void StartConsumerLoop(CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig()
        {
            GroupId = DataAcquisitionConstants.ServiceName,
            EnableAutoCommit = false
        };

        using var consumer = _kafkaConsumerFactory.CreateConsumer(config);
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var _retryRepository = scope.ServiceProvider.GetRequiredService<IRetryRepository>();

            consumer.Subscribe(new List<string>() { KafkaTopic.DataAcquisitionRequestedRetry.GetStringValue(), KafkaTopic.PatientCensusScheduledRetry.GetStringValue() });

            _logger.LogInformation("Started Data Acquisition Service Retry consumer for topics: [{1}] {2}", string.Join(", ", consumer.Subscription), DateTime.UtcNow);

            while (!cancellationToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? consumeResult;

                try
                {
                    await consumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                    {
                        consumeResult = result;

                        try
                        {
                            if (consumeResult.Message.Headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionService, out var exceptionService))
                            {
                                //If retry event is not from the Query Dispatch service, disregard the retry event
                                if (Encoding.UTF8.GetString(exceptionService) != "QueryDispatch")
                                {
                                    consumer.Commit(consumeResult);
                                    //continue;
                                }
                            }

                            if (consumeResult.Message.Headers.TryGetLastBytes(KafkaConstants.HeaderConstants.RetryCount, out var retryCount))
                            {
                                int countValue = int.Parse(Encoding.UTF8.GetString(retryCount));

                                //Dead letter if the retry count exceeds the configured retry duration count
                                if (countValue >= _consumerSettings.Value.ConsumerRetryDuration.Count())
                                {
                                    throw new DeadLetterException($"Retry count exceeded for message with key: {consumeResult.Message.Key}", AuditEventType.Create);
                                }
                            }

                            var retryEntity = _retryEntityFactory.CreateRetryEntity(consumeResult, _consumerSettings.Value);

                            await _retryRepository.AddAsync(retryEntity, cancellationToken);

                            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

                            await RetryScheduleService.CreateJobAndTrigger(retryEntity, scheduler);

                            consumer.Commit(consumeResult);
                        }
                        catch (DeadLetterException ex)
                        {
                            var facilityId = GetFacilityIdFromHeader(consumeResult.Message.Headers);
                            _deadLetterExceptionHandler.Topic = consumeResult.Topic.Replace("-Retry", "-Error");
                            _deadLetterExceptionHandler.HandleException(consumeResult, ex, AuditEventType.Create, facilityId);
                            consumer.Commit(consumeResult);
                            //continue;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in Query Dispatch Service Retry consumer for topics: [{1}] at {2}", string.Join(", ", consumer.Subscription), DateTime.UtcNow);
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
                    _logger.LogError(ex, "Error consuming message for topics: [{1}] at {2}", string.Join(", ", consumer.Subscription), DateTime.UtcNow);
                    continue;
                }
            }
        }
        catch (OperationCanceledException oce)
        {
            _logger.LogError(oce, $"Operation Cancelled: {oce.Message}");
            consumer.Close();
            consumer.Dispose();
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
