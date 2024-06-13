using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Services;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using MediatR;
using System.Text;

namespace LantanaGroup.Link.DataAcquisition.Listeners;

public class BaseListener<MessageType, ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType>
    : BackgroundService
    where MessageType : BaseMessage
{
    private readonly ILogger<BaseListener<MessageType, ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType>> _logger;
    private readonly IKafkaConsumerFactory<ConsumeKeyType, ConsumeValueType> _kafkaConsumerFactory;
    private readonly IDeadLetterExceptionHandler<ConsumeKeyType, ConsumeValueType> _deadLetterConsumerHandler;
    private readonly IDeadLetterExceptionHandler<string, string> _deadLetterConsumerErrorHandler;
    private readonly ITransientExceptionHandler<ConsumeKeyType, ConsumeValueType> _transientExceptionHandler;
    private readonly IConsumerCustomLogic<ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType> _customLogic;

    public BaseListener(
        ILogger<BaseListener<MessageType, ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType>> logger,
        IKafkaConsumerFactory<ConsumeKeyType, ConsumeValueType> kafkaConsumerFactory,
        IKafkaProducerFactory<ProduceKeyType, ProduceValueType> kafkaProducerFactory,
        IDeadLetterExceptionHandler<ConsumeKeyType, ConsumeValueType> deadLetterConsumerHandler,
        IDeadLetterExceptionHandler<string, string> deadLetterConsumerErrorHandler,
        ITransientExceptionHandler<ConsumeKeyType, ConsumeValueType> transientExceptionHandler
,
        IConsumerCustomLogic<ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType> customLogic)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentNullException(nameof(kafkaConsumerFactory));
        _deadLetterConsumerHandler = deadLetterConsumerHandler ?? throw new ArgumentNullException(nameof(deadLetterConsumerHandler));
        _deadLetterConsumerErrorHandler = deadLetterConsumerErrorHandler ?? throw new ArgumentNullException(nameof(deadLetterConsumerErrorHandler));
        _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentNullException(nameof(transientExceptionHandler));
        _customLogic = customLogic ?? throw new ArgumentNullException(nameof(customLogic));

        var messageType = typeof(MessageType).Name;

        //configure error handlers topic names
        _deadLetterConsumerErrorHandler.Topic = $"{messageType}-Error";
        _deadLetterConsumerErrorHandler.Topic = $"{messageType}-Error";
        _transientExceptionHandler.Topic = $"{messageType}-Retry";

        //configure error handlers service names
        _deadLetterConsumerErrorHandler.ServiceName = ServiceActivitySource.ServiceName;
        _deadLetterConsumerHandler.ServiceName = ServiceActivitySource.ServiceName;
        _transientExceptionHandler.ServiceName = ServiceActivitySource.ServiceName;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() => StartConsumerLoop(cancellationToken), cancellationToken);
    }

    private async Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        var settings = new ConsumerConfig
        {
            EnableAutoCommit = false,
            GroupId = DataAcquisitionConstants.ServiceName
        };

        using var consumer = _kafkaConsumerFactory.CreateConsumer(settings);

        try
        {
            var messageType = typeof(MessageType).Name;
            _logger.LogInformation("Starting Consumer Loop for {ServiceName} on topic {topic}", DataAcquisitionConstants.ServiceName, messageType);

            consumer.Subscribe(new string[] { messageType } );

            ConsumeResult<ConsumeKeyType, ConsumeValueType>? consumeResult = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await consumer.ConsumeWithInstrumentation(async (result, CancellationToken) =>
                    {
                        consumeResult = result;

                        try
                        {
                            if(consumeResult != null)
                            {
                                await _customLogic.executeCustomLogic(consumeResult);
                                consumer.Commit(consumeResult);
                            }
                        }
                        catch (DeadLetterException ex)
                        {
                            _deadLetterConsumerHandler.Topic = consumeResult?.Topic + "-Error";
                            _deadLetterConsumerHandler.HandleException(consumeResult, ex, _customLogic.extractFacilityId(consumeResult));
                            consumer.Commit(consumeResult);
                        }
                        catch (TransientException ex)
                        {
                            _transientExceptionHandler.Topic = consumeResult?.Topic + "-Retry";
                            _transientExceptionHandler.HandleException(consumeResult, ex, _customLogic.extractFacilityId(consumeResult));
                            consumer.Commit(consumeResult);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to process Patient Event.");

                            _deadLetterConsumerHandler.HandleException(consumeResult, new DeadLetterException("Data Acquisition Exception thrown: " + ex.Message, AuditEventType.Create), _customLogic.extractFacilityId(consumeResult));
                            consumer.Commit(consumeResult);
                        }
                    }, cancellationToken);
                }
                catch (ConsumeException e)
                {
                    if (e.Error.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        throw new OperationCanceledException(e.Error.Reason, e);
                    }

                    var facilityId = e.ConsumerRecord.Message.Key != null ? _customLogic.extractFacilityId(consumeResult) : "";

                    var converted_record = new ConsumeResult<string, string>()
                    {
                        Message = new Message<string, string>()
                        {
                            Key = facilityId,
                            Value = e.ConsumerRecord.Message.Value != null ? Encoding.UTF8.GetString(e.ConsumerRecord.Message.Value) : "",
                            Headers = e.ConsumerRecord.Message.Headers
                        }
                    };

                    _deadLetterConsumerErrorHandler.HandleException(converted_record, new DeadLetterException("Consume Result exception: " + e.InnerException.Message, AuditEventType.Create), facilityId);

                    consumer.Commit();
                    continue;
                }
                catch (Exception ex)
                {
                    _deadLetterConsumerHandler.HandleException(consumeResult, ex, AuditEventType.Query, "");
                    continue;
                } 
            }
        }
        catch (OperationCanceledException oce)
        {
            _logger.LogError(oce, "Operation Canceled: {1}", oce.Message);
            consumer.Close();
            consumer.Dispose();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
