using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Shared.Application;
public abstract class BaseListener<MessageType, ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType>
    : BackgroundService
{
    protected readonly ILogger<BaseListener<MessageType, ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType>> Logger;
    protected readonly IKafkaConsumerFactory<ConsumeKeyType, ConsumeValueType> KafkaConsumerFactory;
    protected readonly IDeadLetterExceptionHandler<MessageType, ConsumeKeyType, ConsumeValueType> DeadLetterConsumerHandler;
    protected readonly ITransientExceptionHandler<MessageType, ConsumeKeyType, ConsumeValueType> TransientExceptionHandler;
    protected readonly ServiceInformation ServiceInformation;
    protected readonly string TopicName;

    protected BaseListener(
        ILogger<BaseListener<MessageType, ConsumeKeyType, ConsumeValueType, ProduceKeyType, ProduceValueType>> logger,
        IKafkaConsumerFactory<ConsumeKeyType, ConsumeValueType> kafkaConsumerFactory,
        IDeadLetterExceptionHandler<MessageType, ConsumeKeyType, ConsumeValueType> deadLetterConsumerHandler,
        IDeadLetterExceptionHandler<MessageType, string, string> deadLetterConsumerErrorHandler,
        ITransientExceptionHandler<MessageType, ConsumeKeyType, ConsumeValueType> transientExceptionHandler,
        ServiceInformation serviceInformation)
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
            Logger.LogInformation("Starting Consumer Loop for {ServiceName} on topic {topic}", ServiceInformation.ServiceConfigName, this.TopicName);

            consumer.Subscribe(new string[] { this.TopicName });

            ConsumeResult<ConsumeKeyType, ConsumeValueType>? consumeResult = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                    {
                        consumeResult = result;
                        using var stage = StartPipelineStage(consumeResult);

                        try
                        {
                            if (consumeResult != null)
                            {
                                await ExecuteListenerAsync(consumeResult, consumeCancellationToken);
                            }
                            stage?.SetOutcome(PipelineStageOutcome.Completed);
                        }
                        catch (DeadLetterException ex)
                        {
                            stage?.SetOutcome(PipelineStageOutcome.DeadLettered);
                            DeadLetterConsumerHandler.HandleException(consumeResult, ex, ExtractFacilityId(consumeResult));
                        }
                        catch (TransientException ex)
                        {
                            stage?.SetOutcome(PipelineStageOutcome.Retried);
                            TransientExceptionHandler.HandleException(consumeResult, ex, ExtractFacilityId(consumeResult));
                        }
                        catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
                        {
                            stage?.SetOutcome(PipelineStageOutcome.Cancelled);
                            throw;
                        }
                        catch (Exception ex)
                        {
                            stage?.SetOutcome(PipelineStageOutcome.Retried);
                            Logger.LogError(ex,
                                "Unhandled exception in listener for {ServiceName} on topic {Topic}",
                                ServiceInformation.ServiceConfigName, this.TopicName);

                            TransientExceptionHandler.HandleException(consumeResult, new TransientException($"{ServiceInformation.ServiceConfigName} Exception thrown: " + ex.Message, ex), ExtractFacilityId(consumeResult));
                        }
                        finally
                        {
                            if (!consumeCancellationToken.IsCancellationRequested)
                                consumer.SafeCommit(consumeResult, Logger);
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
                    consumer.SafeCommit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset }, Logger);
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Kafka client error in {ServiceName} on topic {Topic}",
                        ServiceInformation.ServiceConfigName, this.TopicName);
                }
            }
        }
        catch (OperationCanceledException oce)
        {
            Logger.LogError(oce, "Operation Canceled: {Message}", oce.Message);
            consumer.Close();
            consumer.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "BaseListener Exception Encountered: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// The pipeline stage this listener is, for the per-patient stage log line (see
    /// <see cref="PipelineStageLog"/>). Null, the default, writes no line: a listener that handles many
    /// messages per patient, or isn't a pipeline stage, should leave it null.
    /// </summary>
    protected virtual string? PipelineStageName => null;

    /// <summary>
    /// The patient, report ids and phase of a message, for the stage line. Called only when
    /// <see cref="PipelineStageName"/> is set; it must not throw.
    /// </summary>
    protected virtual (string? PatientId, IEnumerable<string?>? ReportIds, string? Phase) DescribePipelineMessage(
        ConsumeResult<ConsumeKeyType, ConsumeValueType> consumeResult) => (null, null, null);

    private PipelineStageLog? StartPipelineStage(ConsumeResult<ConsumeKeyType, ConsumeValueType>? consumeResult)
    {
        if (PipelineStageName is not { } stageName || consumeResult is null)
        {
            return null;
        }

        string? facilityId = null, correlationId = null, patientId = null, phase = null;
        IEnumerable<string?>? reportIds = null;
        try
        {
            facilityId = ExtractFacilityId(consumeResult);
            correlationId = ExtractCorrelationId(consumeResult);
        }
        catch (Exception)
        {
            // A message without these ids is dead-lettered by the listener; the stage line still records it.
        }

        try
        {
            (patientId, reportIds, phase) = DescribePipelineMessage(consumeResult);
        }
        catch (Exception)
        {
            // Describing the message is for logging only and must never stop it being processed.
        }

        return PipelineStageLog.Start(Logger, stageName, consumeResult, facilityId, patientId, correlationId, reportIds, phase);
    }

    protected abstract ConsumerConfig CreateConsumerConfig();
    protected abstract string ExtractFacilityId(ConsumeResult<ConsumeKeyType, ConsumeValueType> consumeResult);
    protected abstract string ExtractCorrelationId(ConsumeResult<ConsumeKeyType, ConsumeValueType> consumeResult);
    protected abstract Task ExecuteListenerAsync(ConsumeResult<ConsumeKeyType, ConsumeValueType> consumeResult, CancellationToken cancellationToken = default);
}
