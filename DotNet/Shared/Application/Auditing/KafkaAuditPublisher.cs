using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Shared.Application.Auditing
{
    /// <summary>
    /// Publishes an audit event straight to the AuditableEventOccurred topic, awaiting the delivery
    /// report and retrying before giving up.
    /// </summary>
    /// <remarks>
    /// This runs after the change is committed, so a failure here cannot undo it: the job is to try
    /// hard and then account for what was lost. It never throws - turning a successful write into a
    /// 500 would trade a missing audit row for a confused user and a retried write. The
    /// transactional outbox feature replaces this class.
    /// </remarks>
    public sealed class KafkaAuditPublisher : IAuditPublisher
    {
        private readonly IProducer<string, AuditEventMessage> _producer;
        private readonly IAuditMetrics _metrics;
        private readonly AuditOptions _options;
        private readonly ILogger<KafkaAuditPublisher> _logger;

        public KafkaAuditPublisher(IProducer<string, AuditEventMessage> producer, IAuditMetrics metrics,
            IOptions<AuditOptions> options, ILogger<KafkaAuditPublisher> logger)
        {
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task PublishAsync(AuditEventMessage message, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);

            var publish = _options.Publish;
            var waited = Stopwatch.StartNew();

            for (var attempt = 1; ; attempt++)
            {
                var produce = Produce(message);

                Exception failure;
                try
                {
                    var result = await produce.WaitAsync(Remaining(publish, waited), cancellationToken);

                    //a produce can come back Possibly- or NotPersisted without throwing.
                    //PossiblyPersisted is retried on purpose: a duplicate audit row does less harm
                    //than a missing one.
                    if (result.Status == PersistenceStatus.Persisted)
                    {
                        return;
                    }

                    failure = new KafkaException(new Confluent.Kafka.Error(ErrorCode.Local_Fail,
                        $"The broker did not confirm the audit event: {result.Status}."));
                }
                catch (TimeoutException)
                {
                    //the producer still holds the message and keeps trying, so stop holding the
                    //caller. Producing again here would duplicate the event once the broker is back.
                    Settle(produce, message, attempt, $"not confirmed within {publish.ResponseTimeout}");
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    Settle(produce, message, attempt, "the service is shutting down");
                    return;
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                if (attempt >= publish.MaxAttempts || cancellationToken.IsCancellationRequested)
                {
                    Abandon(message, failure, attempt);
                    return;
                }

                _logger.LogWarning(failure,
                    "Audit event for {Action} {Resource} was not published on attempt {Attempt} of {MaxAttempts}; retrying.",
                    message.Action, message.Resource, attempt, publish.MaxAttempts);

                var backoff = publish.Backoff(attempt);
                if (backoff >= Remaining(publish, waited))
                {
                    //no time left to wait and try again inside the response budget.
                    Abandon(message, failure, attempt);
                    return;
                }

                try
                {
                    await Task.Delay(backoff, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Abandon(message, failure, attempt);
                    return;
                }
            }
        }

        private static TimeSpan Remaining(AuditPublishOptions publish, Stopwatch waited)
        {
            var remaining = publish.ResponseTimeout - waited.Elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        /// <summary>
        /// Starts the produce without the request's token: cancelling ProduceAsync abandons the
        /// delivery report, not the message, so the event would be sent with nobody left to learn
        /// whether it arrived. The caller's token bounds the wait instead.
        /// </summary>
        private Task<DeliveryResult<string, AuditEventMessage>> Produce(AuditEventMessage message)
        {
            try
            {
                return _producer.ProduceAsync(nameof(KafkaTopic.AuditableEventOccurred), BuildMessage(message),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                //a serialisation or configuration failure surfaces before any task exists.
                return Task.FromException<DeliveryResult<string, AuditEventMessage>>(ex);
            }
        }

        /// <summary>
        /// Lets the request return while the delivery report is still outstanding, and accounts for
        /// the event when it finally arrives: nothing if the broker confirmed it late, the counter
        /// and a full log line if it never did.
        /// </summary>
        private void Settle(Task<DeliveryResult<string, AuditEventMessage>> produce, AuditEventMessage message,
            int attempts, string reason)
        {
            if (!produce.IsCompleted)
            {
                _logger.LogWarning(
                    "Audit event for {Action} {Resource} {Reason}; the request is returning and delivery continues in the background.",
                    message.Action, message.Resource, reason);
            }

            _ = produce.ContinueWith(settled =>
            {
                if (settled.IsCompletedSuccessfully && settled.Result.Status == PersistenceStatus.Persisted)
                {
                    _logger.LogInformation("Audit event for {Action} {Resource} was confirmed by the broker after the request returned.",
                        message.Action, message.Resource);
                    return;
                }

                var failure = settled.Exception?.GetBaseException()
                    ?? new KafkaException(new Confluent.Kafka.Error(ErrorCode.Local_Fail, settled.IsCanceled
                        ? "The audit event's delivery was cancelled."
                        : $"The broker did not confirm the audit event: {settled.Result.Status}."));

                Abandon(message, failure, attempts);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private static Message<string, AuditEventMessage> BuildMessage(AuditEventMessage message)
        {
            Headers headers = [];

            //REST audit events carry no correlation id, but the pipeline senders that still exist do
            //set one, and the header costs nothing to keep.
            if (!string.IsNullOrEmpty(message.CorrelationId))
            {
                headers.Add(KafkaConstants.HeaderConstants.CorrelationId,
                    Encoding.ASCII.GetBytes(message.CorrelationId));
            }

            return new Message<string, AuditEventMessage>
            {
                Headers = headers,
                Key = message.FacilityId ?? string.Empty,
                Value = message
            };
        }

        /// <summary>
        /// Everything that can still be done for an event that will never reach Kafka: count it, and
        /// write it out whole so it can be replayed by hand from the log.
        /// </summary>
        /// <remarks>
        /// Whole includes the caller's <c>User</c> and any <c>PatientId</c>, because a partial
        /// record cannot be replayed and replay is all that is left to salvage. The cost is in
        /// dev-docs/auditing-known-limits.md.
        /// </remarks>
        private void Abandon(AuditEventMessage message, Exception ex, int attempts)
        {
            _metrics.PublishFailed(message.Resource);

            string serialised;
            try
            {
                serialised = JsonSerializer.Serialize(message, LogSafeJson);
            }
            catch (Exception serialisationFailure)
            {
                //losing the event is bad enough; losing the log line about it would be worse.
                serialised =
                    $"(the event could not be serialised: {serialisationFailure.Message}) " +
                    $"Action={message.Action} Resource={message.Resource} ResourceId={message.ResourceId} " +
                    $"FacilityId={message.FacilityId} EventDate={message.EventDate:O}";
            }

            //a no-op on the serialised path, by LogSafeJson's encoder; it is for the fallback above,
            //whose interpolated values no serialiser has escaped.
            _logger.LogError(ex,
                "Audit event abandoned after {Attempts} attempt(s); the change it describes is already committed. Event: {AuditEvent}",
                attempts, serialised.SanitizeForLog());
        }

        /// <summary>
        /// The options the abandoned-event line is serialised with, so that line is safe to log and
        /// still replayable.
        /// </summary>
        /// <remarks>
        /// <see cref="JavaScriptEncoder.Default"/> escapes control characters and everything outside
        /// ASCII as <c>\uXXXX</c>, unindented, so the line cannot forge a log entry and
        /// <c>SanitizeForLog</c> has nothing left to replace. On unescaped JSON that same call would
        /// blank every accented character, so this is set explicitly: switching to
        /// <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> would make the sanitising lossy
        /// and the line forgeable at once, without looking like it broke anything.
        /// </remarks>
        private static readonly JsonSerializerOptions LogSafeJson = new()
        {
            Encoder = JavaScriptEncoder.Default,
            WriteIndented = false
        };
    }
}
