using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Retention;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.Report.Listeners;

/// <summary>
/// Consumes <c>RetentionCheckComplete</c> and applies each participant's results to its cleanup.
/// See dev-docs/retention-cleanup-record.md.
/// </summary>
public class RetentionCheckCompleteListener : BackgroundService
{
    // Retention messages carry no facility; the error handlers take one only for their headers.
    private const string NoFacility = "";

    private readonly ILogger<RetentionCheckCompleteListener> _logger;
    private readonly IKafkaConsumerFactory<string, RetentionCheckCompleteValue> _consumerFactory;
    private readonly ServiceInformation _serviceInformation;
    private readonly IDeadLetterExceptionHandler<RetentionCheckCompleteListener, string, string> _consumeExceptionHandler;
    private readonly IDeadLetterExceptionHandler<RetentionCheckCompleteListener, string, RetentionCheckCompleteValue>
        _deadLetterExceptionHandler;
    private readonly ITransientExceptionHandler<RetentionCheckCompleteListener, string, RetentionCheckCompleteValue>
        _transientExceptionHandler;
    private readonly IExceptionLogger<RetentionCheckCompleteListener> _exceptionLogger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// Creates the listener and points its error handlers at the topic's <c>-Error</c> and <c>-Retry</c>
    /// companions.
    /// </summary>
    public RetentionCheckCompleteListener(
        ILogger<RetentionCheckCompleteListener> logger,
        IKafkaConsumerFactory<string, RetentionCheckCompleteValue> consumerFactory,
        ServiceInformation serviceInformation,
        IDeadLetterExceptionHandler<RetentionCheckCompleteListener, string, string> consumeExceptionHandler,
        IDeadLetterExceptionHandler<RetentionCheckCompleteListener, string, RetentionCheckCompleteValue>
            deadLetterExceptionHandler,
        ITransientExceptionHandler<RetentionCheckCompleteListener, string, RetentionCheckCompleteValue>
            transientExceptionHandler,
        IExceptionLogger<RetentionCheckCompleteListener> exceptionLogger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _consumerFactory = consumerFactory ?? throw new ArgumentNullException(nameof(consumerFactory));
        _serviceInformation = serviceInformation ?? throw new ArgumentNullException(nameof(serviceInformation));
        _consumeExceptionHandler = consumeExceptionHandler ??
            throw new ArgumentNullException(nameof(consumeExceptionHandler));
        _deadLetterExceptionHandler = deadLetterExceptionHandler ??
            throw new ArgumentNullException(nameof(deadLetterExceptionHandler));
        _transientExceptionHandler = transientExceptionHandler ??
            throw new ArgumentNullException(nameof(transientExceptionHandler));
        _exceptionLogger = exceptionLogger ?? throw new ArgumentNullException(nameof(exceptionLogger));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));

        // An unset topic makes the handlers drop a poison message into a log line instead of -Error.
        _consumeExceptionHandler.Topic = KafkaTopic.RetentionCheckCompleteError.GetStringValue();
        _deadLetterExceptionHandler.Topic = KafkaTopic.RetentionCheckCompleteError.GetStringValue();
        _transientExceptionHandler.Topic = KafkaTopic.RetentionCheckCompleteRetry.GetStringValue();
    }

    /// <summary>
    /// How long the listener waits before polling again while <c>RetentionCheckComplete</c> does not exist yet.
    /// </summary>
    public TimeSpan MissingTopicRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
    }

    /// <summary>
    /// Applies one completion. Malformed messages throw <see cref="DeadLetterException"/>.
    /// </summary>
    private async Task ConsumeMessageAsync(ConsumeResult<string, RetentionCheckCompleteValue> result,
                                            CancellationToken cancellationToken)
    {
        var completion = result.Message.Value;

        if (completion is null ||
            string.IsNullOrWhiteSpace(completion.ServiceName) ||
            !Guid.TryParse(completion.RetentionCleanupId, out var retentionCleanupId) ||
            !RetentionCleanupRules.HasDefinedStatuses(completion))
        {
            throw new DeadLetterException(
                "Invalid RetentionCheckComplete message: it needs a ServiceName, a RetentionCleanupId that is " +
                "a UUID, and a Status of Completed or Failed on every result.");
        }

        if (!string.Equals(result.Message.Key, completion.RetentionCleanupId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "RetentionCheckComplete from {ServiceName} is keyed {Key} but carries cleanup " +
                "{RetentionCleanupId}; using the value.",
                completion.ServiceName.SanitizeForLog(),
                result.Message.Key.SanitizeForLog(),
                retentionCleanupId);
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRetentionCleanupManager>();

        await manager.ApplyCompletionAsync(retentionCleanupId, completion, cancellationToken);
    }

    private async Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        using var kafkaConsumer = _consumerFactory.CreateConsumer(new ConsumerConfig
        {
            GroupId = _serviceInformation.ServiceConfigName,
            EnableAutoCommit = false
        });

        kafkaConsumer.Subscribe(nameof(KafkaTopic.RetentionCheckComplete));
        var missingTopicReported = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await kafkaConsumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                {
                    missingTopicReported = false;

                    if (result is null)
                    {
                        throw new DeadLetterException(
                            $"Received null message from topic '{nameof(KafkaTopic.RetentionCheckComplete)}'.");
                    }

                    // Every branch commits; an exception escaping here would stop the whole Report host.
                    try
                    {
                        await ConsumeMessageAsync(result, consumeCancellationToken);
                        kafkaConsumer.SafeCommit(result, _logger);
                    }
                    catch (DeadLetterException ex)
                    {
                        _deadLetterExceptionHandler.HandleException(result, ex, NoFacility);
                        kafkaConsumer.SafeCommit(result, _logger);
                    }
                    catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
                    {
                        // Left uncommitted so the completion is redelivered; applying it again is idempotent.
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // Database faults and anything unclassified are retried through -Retry.
                        _transientExceptionHandler.HandleException(result, ex, NoFacility);
                        kafkaConsumer.SafeCommit(result, _logger);
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
            {
                // Topics are created by a sync run by hand, so Report can start before this one exists. Letting
                // the error escape would stop the whole Report host; wait and poll again instead.
                if (!missingTopicReported)
                {
                    _logger.LogError(
                        "Topic {Topic} does not exist yet, so retention completions cannot be consumed; " +
                        "polling again every {RetryDelay}.",
                        nameof(KafkaTopic.RetentionCheckComplete),
                        MissingTopicRetryDelay);
                    missingTopicReported = true;
                }
                else
                {
                    _logger.LogDebug("Topic {Topic} still does not exist.", nameof(KafkaTopic.RetentionCheckComplete));
                }

                try
                {
                    await Task.Delay(MissingTopicRetryDelay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            }
            catch (ConsumeException ex)
            {
                _consumeExceptionHandler.HandleConsumeException(ex, NoFacility);
                var offset = ex.ConsumerRecord?.TopicPartitionOffset;

                kafkaConsumer.SafeCommit(
                    offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset },
                    _logger);
            }
            catch (Exception ex)
            {
                // The backstop for faults outside the per-message handling, such as a failed commit.
                _exceptionLogger.Handle(ex,
                                        $"Error encountered in {nameof(RetentionCheckCompleteListener)}",
                                        LogLevel.Error);
            }
        }
    }
}
