using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Listeners;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Submission.KafkaProducers;

namespace LantanaGroup.Link.Submission.Application.Error;

/// <summary>
/// Retries that are used up are dead-lettered here, not in <c>SubmitPayloadListener</c>.
/// That is the other place a patient submission finally fails.
/// </summary>
public sealed class SubmitPayloadRetryDeadLetterHandler : IDeadLetterExceptionHandler<RetryListener, string, string>
{
    public const string ErrorTopic = "SubmitPayload-Error";

    private readonly DeadLetterExceptionHandler<RetryListener, string, string> _inner;
    private readonly SubmissionPatientStatusPublisher _patientStatus;

    public SubmitPayloadRetryDeadLetterHandler(
        DeadLetterExceptionHandler<RetryListener, string, string> inner,
        SubmissionPatientStatusPublisher patientStatus)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _patientStatus = patientStatus ?? throw new ArgumentNullException(nameof(patientStatus));
    }

    public string Topic
    {
        get => _inner.Topic;
        set => _inner.Topic = value;
    }

    public void HandleException(ConsumeResult<string, string> consumeResult, string facilityId, string message = "")
    {
        PublishIfTerminal(consumeResult, message);
        _inner.HandleException(consumeResult, facilityId, message);
    }

    public void HandleException(ConsumeResult<string, string> consumeResult, Exception ex, string facilityId)
    {
        PublishIfTerminal(consumeResult, ex.Message);
        _inner.HandleException(consumeResult, ex, facilityId);
    }

    public void HandleException(ConsumeResult<string, string> consumeResult, DeadLetterException ex, string facilityId)
    {
        PublishIfTerminal(consumeResult, ex.Message);
        _inner.HandleException(consumeResult, ex, facilityId);
    }

    public void HandleConsumeException(ConsumeException ex, string facilityId)
    {
        _inner.HandleConsumeException(ex, facilityId);
    }

    public void ProduceDeadLetter(ConsumeResult<string, string> consumeResult, string exceptionMessage)
    {
        PublishIfTerminal(consumeResult, exceptionMessage);
        _inner.ProduceDeadLetter(consumeResult, exceptionMessage);
    }

    private void PublishIfTerminal(ConsumeResult<string, string>? consumeResult, string? fallbackError)
    {
        if (!string.Equals(Topic, ErrorTopic, StringComparison.Ordinal) || consumeResult?.Message == null)
        {
            return;
        }

        if (!SubmissionPatientStatus.TryReadPayload(consumeResult.Message.Key, consumeResult.Message.Value, out var key, out var value))
        {
            return;
        }

        var errorMessage = SubmissionPatientStatus.TerminalErrorMessage(consumeResult.Message.Headers, fallbackError);
        _patientStatus.PublishTerminalFailure(key, value, consumeResult.Message.Headers, errorMessage, CancellationToken.None);
    }
}
