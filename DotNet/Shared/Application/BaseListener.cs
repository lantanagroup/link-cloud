using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace LantanaGroup.Link.Shared.Application;
public abstract class BaseListener<MessageType, ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType>
    : BackgroundService
{
    protected readonly ILogger<BaseListener<MessageType, ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType>> Logger;
    protected readonly IKafkaConsumerFactory<ConsumeKeyType, ConsumeValueType> KafkaConsumerFactory;
    protected readonly IDeadLetterExceptionHandler<ConsumeKeyType, ConsumeValueType> DeadLetterConsumerHandler;
    protected readonly ITransientExceptionHandler<ConsumeKeyType, ConsumeValueType> TransientExceptionHandler;
    protected readonly IOptions<ServiceInformation> ServiceInformation;
    protected readonly string TopicName;

    protected BaseListener(
        ILogger<BaseListener<MessageType, ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType>> logger,
        IKafkaConsumerFactory<ConsumeKeyType, ConsumeValueType> kafkaConsumerFactory,
        IDeadLetterExceptionHandler<ConsumeKeyType, ConsumeValueType> deadLetterConsumerHandler,
        IDeadLetterExceptionHandler<string, string> deadLetterConsumerErrorHandler,
        ITransientExceptionHandler<ConsumeKeyType, ConsumeValueType> transientExceptionHandler,
        IOptions<ServiceInformation> serviceInformation)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        KafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentNullException(nameof(kafkaConsumerFactory));
        DeadLetterConsumerHandler = deadLetterConsumerHandler ?? throw new ArgumentNullException(nameof(deadLetterConsumerHandler));
        TransientExceptionHandler = transientExceptionHandler ?? throw new ArgumentNullException(nameof(transientExceptionHandler));
        ServiceInformation = serviceInformation ?? throw new ArgumentNullException(nameof(serviceInformation));
        this.TopicName = typeof(MessageType).Name;

        //configure error handlers topic names
        DeadLetterConsumerHandler.Topic = $"{this.TopicName}-Error";
        TransientExceptionHandler.Topic = $"{this.TopicName}-Retry";

        //configure error handlers service names
        DeadLetterConsumerHandler.ServiceName = ServiceInformation.Value.ServiceName;
        TransientExceptionHandler.ServiceName = ServiceInformation.Value.ServiceName;
        
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
        var settings = CreateConsumerConfig();
        using var consumer = KafkaConsumerFactory.CreateConsumer(settings);

        try
        {
            Logger.LogInformation("Starting Consumer Loop for {ServiceName} on topic {topic}", ServiceInformation.Value.ServiceName, this.TopicName);

            consumer.Subscribe(new string[] { this.TopicName });

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
                            if (consumeResult != null)
                            {
                                await ExecuteListenerAsync(consumeResult, cancellationToken);
                            }
                        }
                        catch (DeadLetterException ex)
                        {
                            DeadLetterConsumerHandler.HandleException(consumeResult, ex, ExtractFacilityId(consumeResult));
                        }
                        catch (TransientException ex)
                        {
                            TransientExceptionHandler.HandleException(consumeResult, ex, ExtractFacilityId(consumeResult));
                        }
                        catch (Exception ex)
                        {
                            DeadLetterConsumerHandler.HandleException(consumeResult, new DeadLetterException("Data Acquisition Exception thrown: " + ex.Message), ExtractFacilityId(consumeResult));
                        }
                        finally
                        {
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

                    if (consumeResult is null)
                    {
                        throw new OperationCanceledException(e.Error.Reason, e);
                    }

                    var facilityId = ExtractFacilityId(consumeResult);

                    DeadLetterConsumerHandler.HandleConsumeException(e, facilityId);

                    var offset = e.ConsumerRecord?.TopicPartitionOffset;
                    consumer.Commit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset });
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch (Exception ex)
                {
                    DeadLetterConsumerHandler.HandleException(consumeResult, ex, "");


                    if(consumeResult != null) { 
                        consumer.Commit(consumeResult);
                    }
                    else
                    {
                        consumer.Commit();
                    }
                }
            }
        }
        catch (OperationCanceledException oce)
        {
            Logger.LogError(oce, "Operation Canceled: {1}", oce.Message);
            consumer.Close();
            consumer.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "BaseListener Exception Encountered: {1}", ex.Message);
            throw;
        }
    }

    protected abstract ConsumerConfig CreateConsumerConfig();
    protected abstract string ExtractFacilityId(ConsumeResult<ConsumeKeyType, ConsumeValueType> consumeResult);
    protected abstract string ExtractCorrelationId(ConsumeResult<ConsumeKeyType, ConsumeValueType> consumeResult);
    protected abstract Task ExecuteListenerAsync(ConsumeResult<ConsumeKeyType, ConsumeValueType> consumeResult, CancellationToken cancellationToken = default);
}
