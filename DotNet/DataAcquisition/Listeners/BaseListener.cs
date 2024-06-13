using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Services;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using System.Text;

namespace LantanaGroup.Link.DataAcquisition.Listeners;

public class BaseListener<MessageType, ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType>
    : BackgroundService
{
    private readonly ILogger<BaseListener<MessageType, ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType>> _logger;
    private readonly IKafkaConsumerFactory<ConsumeKeyType, ConsumeValueType> _kafkaConsumerFactory;
    private readonly IDeadLetterExceptionHandler<ConsumeKeyType, ConsumeValueType> _deadLetterConsumerHandler;
    private readonly IDeadLetterExceptionHandler<string, string> _deadLetterConsumerErrorHandler;
    private readonly ITransientExceptionHandler<ConsumeKeyType, ConsumeValueType> _transientExceptionHandler;
    private readonly IConsumerLogic<ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType> _consumerLogic;

    public BaseListener(
        ILogger<BaseListener<MessageType, ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType>> logger,
        IKafkaConsumerFactory<ConsumeKeyType, ConsumeValueType> kafkaConsumerFactory,
        IDeadLetterExceptionHandler<ConsumeKeyType, ConsumeValueType> deadLetterConsumerHandler,
        IDeadLetterExceptionHandler<string, string> deadLetterConsumerErrorHandler,
        ITransientExceptionHandler<ConsumeKeyType, ConsumeValueType> transientExceptionHandler,
        IConsumerLogic<ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType> consumerLogic)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentNullException(nameof(kafkaConsumerFactory));
        _deadLetterConsumerHandler = deadLetterConsumerHandler ?? throw new ArgumentNullException(nameof(deadLetterConsumerHandler));
        _deadLetterConsumerErrorHandler = deadLetterConsumerErrorHandler ?? throw new ArgumentNullException(nameof(deadLetterConsumerErrorHandler));
        _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentNullException(nameof(transientExceptionHandler));
        _consumerLogic = consumerLogic ?? throw new ArgumentNullException(nameof(consumerLogic));

        var messageType = typeof(MessageType).Name;

        //configure error handlers topic names
        _deadLetterConsumerErrorHandler.Topic = $"{messageType}-Error";
        _deadLetterConsumerHandler.Topic = $"{messageType}-Error";
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
        var messageType = typeof(MessageType).Name;
        
        using var consumer = _kafkaConsumerFactory.CreateConsumer(_consumerLogic.createConsumerConfig());

        try
        {
            _logger.LogInformation("Starting Consumer Loop for {ServiceName} on topic {topic}", ServiceActivitySource.ServiceName, messageType);

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
                                await _consumerLogic.executeLogic(consumeResult);
                            }
                        }
                        catch (DeadLetterException ex)
                        {
                            _deadLetterConsumerHandler.HandleException(consumeResult, ex, _consumerLogic.extractFacilityId(consumeResult));
                        }
                        catch (TransientException ex)
                        {
                            _transientExceptionHandler.HandleException(consumeResult, ex, _consumerLogic.extractFacilityId(consumeResult));
                        }
                        catch (Exception ex)
                        {
                            _deadLetterConsumerHandler.HandleException(consumeResult, new DeadLetterException("Data Acquisition Exception thrown: " + ex.Message, AuditEventType.Create), _consumerLogic.extractFacilityId(consumeResult)); 
                        }
                        finally
                        {
                            consumer.Commit();
                        }
                    }, cancellationToken);
                }
                catch (ConsumeException e)
                {
                    if (e.Error.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        throw new OperationCanceledException(e.Error.Reason, e);
                    }

                    var facilityId = e.ConsumerRecord.Message.Key != null ? _consumerLogic.extractFacilityId(consumeResult) : "";

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
                    continue;
                }
                catch (OperationCanceledException)
                {
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
