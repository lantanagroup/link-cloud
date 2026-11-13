using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LantanaGroup.Link.Shared.Application.Utilities;

/// <summary>How a pipeline stage finished with one message.</summary>
public enum PipelineStageOutcome
{
    /// <summary>The stage did its work and produced whatever comes next.</summary>
    Completed,
    /// <summary>The stage chose not to process the message, for example because the report was aborted.</summary>
    Skipped,
    /// <summary>The message was sent to the stage's retry topic.</summary>
    Retried,
    /// <summary>The message was sent to the stage's error topic and will not be processed.</summary>
    DeadLettered,
    /// <summary>The stage stopped because the service is shutting down.</summary>
    Cancelled,
    /// <summary>The stage ended without saying how; treated as a failure.</summary>
    Failed
}

/// <summary>
/// The per-patient record of one pipeline stage handling one message. Created when a stage starts on a
/// message and disposed when it is done, it does two things:
/// <list type="bullet">
/// <item>Opens a log scope carrying the facility, patient, correlation and report ids, so every line the
/// stage writes while it works can be found by report or patient.</item>
/// <item>On dispose, writes one Information line saying how the stage ended, how long the message waited
/// in Kafka before the stage picked it up, and how long the stage took.</item>
/// </list>
/// </summary>
/// <remarks>
/// The line's shape is stable on purpose, and the Java services write the same one: it is what a
/// per-stage timing query reads. The <c>:l</c> format renders values without Serilog's quotes, so the
/// .NET and Java lines read the same. It is written once per patient per stage, never per resource.
/// Set <see cref="Outcome"/> on every path; a record disposed without one is logged as
/// <see cref="PipelineStageOutcome.Failed"/>.
/// </remarks>
public sealed class PipelineStageLog : IDisposable
{
    public const string MessageTemplate =
        "Pipeline stage {PipelineStage:l} {PipelineOutcome:l}: facility {FacilityId:l}, patient {PatientId:l}, correlation {CorrelationId:l}, reports {ReportIds:l}, phase {Phase:l}, waited {WaitedMs:l} ms, took {TookMs:l} ms";

    private readonly ILogger _logger;
    private readonly IDisposable? _scope;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly string _stage;
    private readonly string _facilityId;
    private readonly string _patientId;
    private readonly string _correlationId;
    private readonly string _reportIds;
    private readonly string _phase;
    private readonly long? _waitedMs;
    private bool _disposed;

    private PipelineStageLog(ILogger logger, string stage, string? facilityId, string? patientId, string? correlationId,
        IEnumerable<string?>? reportIds, string? phase, DateTime? messageTimestampUtc)
    {
        _logger = logger;
        _stage = Clean(stage);
        _facilityId = Clean(facilityId);
        _patientId = Clean(patientId);
        _correlationId = Clean(correlationId);
        _reportIds = string.Join(",", (reportIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Select(Clean).Distinct());
        if (_reportIds.Length == 0) _reportIds = "-";
        _phase = NormalizePhase(phase);

        if (messageTimestampUtc is { } produced && produced > DateTime.UnixEpoch)
        {
            _waitedMs = Math.Max(0, (long)(DateTime.UtcNow - produced).TotalMilliseconds);
        }

        _scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["PipelineStage"] = _stage,
            ["FacilityId"] = _facilityId,
            ["PatientId"] = _patientId,
            ["CorrelationId"] = _correlationId,
            ["ReportIds"] = _reportIds
        });
    }

    /// <summary>How the stage ended. Set it on every path before the record is disposed.</summary>
    public PipelineStageOutcome? Outcome { get; set; }

    /// <summary>
    /// The stage's duration when it was measured elsewhere, for a stage that spans more than the code
    /// holding this record (acquisition runs many queries; the record is only made at the end). Null
    /// uses the time since the record was started.
    /// </summary>
    public long? TookMsOverride { get; set; }

    /// <summary>Sets <see cref="Outcome"/>; for callers holding a record that may be null.</summary>
    public void SetOutcome(PipelineStageOutcome outcome) => Outcome = outcome;

    /// <summary>
    /// Starts the record for a stage handling a Kafka message. The message's timestamp (set by the
    /// producer) gives how long it waited in Kafka.
    /// </summary>
    public static PipelineStageLog Start<TKey, TValue>(ILogger logger, string stage, ConsumeResult<TKey, TValue>? message,
        string? facilityId, string? patientId, string? correlationId, IEnumerable<string?>? reportIds, string? phase = null)
    {
        DateTime? produced = null;
        var timestamp = message?.Message?.Timestamp;
        if (timestamp is { } ts && ts.Type != TimestampType.NotAvailable)
        {
            produced = ts.UtcDateTime;
        }

        return new PipelineStageLog(logger, stage, facilityId, patientId, correlationId, reportIds, phase, produced);
    }

    /// <summary>Starts the record for a stage that isn't triggered by one Kafka message.</summary>
    public static PipelineStageLog Start(ILogger logger, string stage, string? facilityId, string? patientId,
        string? correlationId, IEnumerable<string?>? reportIds, string? phase = null, DateTime? startedUtc = null)
        => new(logger, stage, facilityId, patientId, correlationId, reportIds, phase, startedUtc);

    /// <summary>
    /// A stage name that includes its query phase, as the Report and Patient State Machine ADR names its
    /// statuses: <c>InitialAcquisition</c>, <c>SupplementalNormalization</c> and so on. A phase other than
    /// initial or supplemental (or none) leaves the name as it is.
    /// </summary>
    public static string PhasedStage(string stage, string? phase)
    {
        if (string.Equals(phase, "initial", StringComparison.OrdinalIgnoreCase)) return "Initial" + stage;
        if (string.Equals(phase, "supplemental", StringComparison.OrdinalIgnoreCase)) return "Supplemental" + stage;
        return stage;
    }

    /// <summary>The last value of a Kafka header as text, or null when it is absent; for logging only.</summary>
    public static string? Header(Headers? headers, string name)
    {
        if (headers is null || !headers.TryGetLastBytes(name, out var bytes) || bytes is null || bytes.Length == 0)
        {
            return null;
        }

        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stopwatch.Stop();

        var outcome = Outcome ?? PipelineStageOutcome.Failed;
        var level = outcome is PipelineStageOutcome.Completed or PipelineStageOutcome.Skipped or PipelineStageOutcome.Cancelled
            ? LogLevel.Information
            : LogLevel.Warning;

        _logger.Log(level, MessageTemplate, _stage, ToName(outcome), _facilityId, _patientId, _correlationId, _reportIds,
            _phase, _waitedMs?.ToString() ?? "-", (TookMsOverride ?? _stopwatch.ElapsedMilliseconds).ToString());

        _scope?.Dispose();
    }

    private static string ToName(PipelineStageOutcome outcome) => outcome switch
    {
        PipelineStageOutcome.Completed => "completed",
        PipelineStageOutcome.Skipped => "skipped",
        PipelineStageOutcome.Retried => "retried",
        PipelineStageOutcome.DeadLettered => "dead-lettered",
        PipelineStageOutcome.Cancelled => "cancelled",
        _ => "failed"
    };

    /// <summary>
    /// The query phases arrive as <c>Initial</c>, <c>INITIAL</c> or <c>SUPPLEMENTAL</c> depending on the
    /// service; they are written in lower case so one query matches every stage. Other phase values (a
    /// measure id, a payload type) keep their case.
    /// </summary>
    private static string NormalizePhase(string? phase)
    {
        var cleaned = Clean(phase);
        return cleaned.Equals("initial", StringComparison.OrdinalIgnoreCase)
               || cleaned.Equals("supplemental", StringComparison.OrdinalIgnoreCase)
            ? cleaned.ToLowerInvariant()
            : cleaned;
    }

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.SanitizeForLog();
}
