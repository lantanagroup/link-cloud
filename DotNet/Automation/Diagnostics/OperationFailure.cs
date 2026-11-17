using Microsoft.Extensions.Logging;

namespace LantanaGroup.Automation.Diagnostics;

/// <summary>
/// One failed automation HTTP operation. Missing identifiers are the literal
/// <see cref="OperationFailureText.NotProvided"/>; this type never invents them.
/// </summary>
public sealed record OperationFailure
{
    public required string Step { get; init; }
    public required string PatientOrChunk { get; init; }
    public required string Method { get; init; }
    public required string SanitizedEndpoint { get; init; }
    public int? StatusCode { get; init; }
    public string? ReasonPhrase { get; init; }
    public required string RespondingSystem { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public required string RunId { get; init; }
    public string? FacilityId { get; init; }
    public required string CorrelationId { get; init; }
    public required string TraceId { get; init; }
    public required string RequestId { get; init; }
    public required string Details { get; init; }
    public required string UnderlyingCause { get; init; }
    public required string TechnicalDetails { get; init; }
    public required string SearchGuidance { get; init; }
    public required string OneLineSummary { get; init; }
    public required string FormattedBlock { get; init; }
}

public static class OperationFailureText
{
    public const string NotProvided = "Not provided";
    public const string UnderlyingCauseNotInResponse = "Not provided in the response.";
    public const int MaxRecordedFailures = 20;

    public const string SearchGuidance =
        "Search the same log UI used by the other Link services for this run ID and trace ID. " +
        "Automation.UI writes these failure warnings there when the Loki sink is configured. " +
        "A FHIR server database exception may still be absent from Link service logs.";
}

/// <summary>
/// Run and facility are read when the failure happens. The loader is shared across
/// concurrent patient uploads, so this context must not store per-request state.
/// </summary>
public sealed class FhirOperationContext
{
    public string? RunId { get; init; }
    public Func<string?>? FacilityId { get; init; }
    public ILogger? Logger { get; init; }
    public Action<OperationFailure>? Record { get; init; }
}

public sealed class OperationFailedException : Exception
{
    public OperationFailure Failure { get; }

    public OperationFailedException(OperationFailure failure)
        : base(failure.OneLineSummary)
    {
        Failure = failure;
    }

    public static IReadOnlyList<OperationFailure> Collect(Exception exception)
    {
        var found = new List<OperationFailure>();
        Walk(exception, found);
        return found;
    }

    private static void Walk(Exception exception, List<OperationFailure> found)
    {
        switch (exception)
        {
            case OperationFailedException operationFailed:
                found.Add(operationFailed.Failure);
                return;
            case AggregateException aggregate:
                foreach (var inner in aggregate.Flatten().InnerExceptions)
                    Walk(inner, found);
                return;
            default:
                if (exception.InnerException != null)
                    Walk(exception.InnerException, found);
                return;
        }
    }
}

public static class OperationFailureLog
{
    public static void Write(ILogger? logger, OperationFailure failure)
    {
        if (logger == null)
            return;

        logger.LogWarning(
            "Operation failed. {Summary} {RunId} {FacilityId} {Step} {PatientOrChunk} {Method} {Endpoint} {StatusCode} {RespondingSystem} {CorrelationId} {TraceId} {RequestId}",
            failure.OneLineSummary,
            failure.RunId,
            string.IsNullOrWhiteSpace(failure.FacilityId) ? OperationFailureText.NotProvided : failure.FacilityId,
            failure.Step,
            failure.PatientOrChunk,
            failure.Method,
            failure.SanitizedEndpoint,
            failure.StatusCode?.ToString() ?? OperationFailureText.NotProvided,
            failure.RespondingSystem,
            failure.CorrelationId,
            failure.TraceId,
            failure.RequestId);

        if (!logger.IsEnabled(LogLevel.Debug))
            return;

        logger.LogDebug(
            "Operation failure detail. {RunId} {TechnicalDetails}",
            failure.RunId,
            FlattenForLog(failure.TechnicalDetails));
    }

    /// <summary>
    /// Matches <c>StringSecurityExtensions.SanitizeForLog</c>: control characters become spaces
    /// so a body cannot forge extra log lines. Secret redaction happens before this.
    /// </summary>
    public static string FlattenForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var ch = chars[i];
            if (ch < 32 || ch == 127)
                chars[i] = ' ';
        }

        return new string(chars);
    }
}
