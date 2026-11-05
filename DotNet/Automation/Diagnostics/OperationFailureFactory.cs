using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace LantanaGroup.Automation.Diagnostics;

public sealed class OperationFailureInput
{
    public required string Name { get; init; }
    public string? ProgressPrefix { get; init; }
    public string? Step { get; init; }
    public required string Method { get; init; }
    public required string Endpoint { get; init; }
    public int? StatusCode { get; init; }
    public string? ReasonPhrase { get; init; }
    public string? ResponseBody { get; init; }
    public string? TraceParent { get; init; }
    public string? TraceIdHeader { get; init; }
    /// <summary>Trace id from an activity that was actually sent. Never a newly created id.</summary>
    public string? SentTraceId { get; init; }
    public string? CorrelationId { get; init; }
    public string? RequestId { get; init; }
    public string? RunId { get; init; }
    public string? FacilityId { get; init; }
    public string? RespondingSystem { get; init; }
    public DateTimeOffset? TimestampUtc { get; init; }
}

public static class OperationFailureFactory
{
    public const int MaxDetailsChars = 500;
    public const int MaxTechnicalDetailsChars = 4000;

    private static readonly Regex BearerRegex = new(
        @"Bearer\s+\S+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AuthorizationRegex = new(
        @"(Authorization\s*[:=]\s*)(?:Bearer\s+)?\S+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex JsonSecretRegex = new(
        @"(""(?:token|access_token|client_secret|password|authorization)""\s*:\s*"")[^""]*""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex QuerySecretRegex = new(
        @"([?&])(token|access_token|code|client_secret|password|authorization)=([^&#\s]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static OperationFailure FromHttp(OperationFailureInput input)
    {
        var step = string.IsNullOrWhiteSpace(input.Step)
            ? InferStep(input.ProgressPrefix, input.Name)
            : input.Step.Trim();
        var name = string.IsNullOrWhiteSpace(input.Name)
            ? OperationFailureText.NotProvided
            : input.Name.Trim();
        var method = string.IsNullOrWhiteSpace(input.Method)
            ? OperationFailureText.NotProvided
            : input.Method.Trim().ToUpperInvariant();
        var endpoint = SanitizeEndpoint(input.Endpoint);
        var statusText = input.StatusCode?.ToString() ?? OperationFailureText.NotProvided;
        var reason = string.IsNullOrWhiteSpace(input.ReasonPhrase) ? null : input.ReasonPhrase.Trim();
        var timestamp = (input.TimestampUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var runId = Provided(input.RunId);
        var facilityId = string.IsNullOrWhiteSpace(input.FacilityId) ? null : input.FacilityId.Trim();
        var correlationId = Provided(input.CorrelationId);
        var requestId = Provided(input.RequestId);
        var traceId = Provided(First(
            TraceIdFromTraceParent(input.TraceParent),
            input.TraceIdHeader,
            input.SentTraceId));
        var respondingSystem = string.IsNullOrWhiteSpace(input.RespondingSystem)
            ? OperationFailureText.NotProvided
            : input.RespondingSystem.Trim();

        var diagnostics = TryReadOperationOutcomeDiagnostics(input.ResponseBody);
        var details = diagnostics != null
            ? Cap(Collapse(RedactSecrets(diagnostics)), MaxDetailsChars)
            : StatusSentence(input.StatusCode, reason, input.ResponseBody);
        var underlying = UnderlyingCause(diagnostics ?? input.ResponseBody);
        var technical = SanitizeBody(input.ResponseBody);
        var summary = OneLine(step, name, statusText);
        var block = FormatBlock(
            summary,
            step,
            name,
            method,
            endpoint,
            StatusLine(input.StatusCode, reason),
            respondingSystem,
            timestamp,
            runId,
            facilityId,
            correlationId,
            traceId,
            requestId,
            details,
            underlying,
            technical);

        return new OperationFailure
        {
            Step = step,
            PatientOrChunk = name,
            Method = method,
            SanitizedEndpoint = endpoint,
            StatusCode = input.StatusCode,
            ReasonPhrase = reason,
            RespondingSystem = respondingSystem,
            TimestampUtc = timestamp,
            RunId = runId,
            FacilityId = facilityId,
            CorrelationId = correlationId,
            TraceId = traceId,
            RequestId = requestId,
            Details = details,
            UnderlyingCause = underlying,
            TechnicalDetails = technical,
            SearchGuidance = OperationFailureText.SearchGuidance,
            OneLineSummary = summary,
            FormattedBlock = block
        };
    }

    public static string InferStep(string? progressPrefix, string? name)
    {
        var prefix = progressPrefix ?? string.Empty;
        var bundleName = name ?? string.Empty;
        if (prefix.Contains("[shared]", StringComparison.OrdinalIgnoreCase)
            || bundleName.StartsWith("shared", StringComparison.OrdinalIgnoreCase))
            return "Upload shared infrastructure";
        if (prefix.Contains("[imported:", StringComparison.OrdinalIgnoreCase))
            return "Upload imported patient";
        if (prefix.Contains("[replace:", StringComparison.OrdinalIgnoreCase))
            return "Replace patient bundle";
        return "Upload patient chunk";
    }

    public static string SanitizeEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return OperationFailureText.NotProvided;

        var trimmed = endpoint.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var builder = new UriBuilder(uri)
            {
                UserName = string.Empty,
                Password = string.Empty
            };
            trimmed = builder.Uri.AbsoluteUri;
        }

        return RedactSecrets(trimmed);
    }

    public static string SanitizeBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return OperationFailureText.NotProvided;

        var text = RedactSecrets(body);
        if (text.Length <= MaxTechnicalDetailsChars)
            return text;

        return text[..MaxTechnicalDetailsChars] + "\n[truncated]";
    }

    internal static string RedactSecrets(string text)
    {
        text = BearerRegex.Replace(text, "Bearer [redacted]");
        text = AuthorizationRegex.Replace(text, "$1[redacted]");
        text = JsonSecretRegex.Replace(text, "$1[redacted]\"");
        text = QuerySecretRegex.Replace(text, "$1$2=[redacted]");
        text = Regex.Replace(text, @"(?<=://)[^/\s:@]+:[^/\s@]+@", string.Empty);
        return text;
    }

    private static string OneLine(string step, string name, string statusText) => step switch
    {
        "Upload patient chunk" => $"Patient chunk failed: {name} (HTTP {statusText})",
        "Upload shared infrastructure" => $"Shared infrastructure upload failed: {name} (HTTP {statusText})",
        "Upload imported patient" => $"Imported patient upload failed: {name} (HTTP {statusText})",
        "Replace patient bundle" => $"Patient replacement failed: {name} (HTTP {statusText})",
        _ => $"{step} failed: {name} (HTTP {statusText})"
    };

    private static string FormatBlock(
        string summary,
        string step,
        string name,
        string method,
        string endpoint,
        string statusLine,
        string respondingSystem,
        DateTimeOffset timestamp,
        string runId,
        string? facilityId,
        string correlationId,
        string traceId,
        string requestId,
        string details,
        string underlying,
        string technical)
    {
        var lines = new List<string>
        {
            summary,
            $"Step: {step}",
            $"Patient or chunk: {name}",
            $"Request: {method} {endpoint}",
            $"HTTP status: {statusLine}",
            $"Responding system: {respondingSystem}",
            $"Timestamp (UTC): {timestamp:yyyy-MM-ddTHH:mm:ss.fffffffZ}",
            $"Run ID: {runId}"
        };
        if (!string.IsNullOrWhiteSpace(facilityId))
            lines.Add($"Facility ID: {facilityId}");
        lines.Add($"Correlation ID: {correlationId}");
        lines.Add($"Trace ID: {traceId}");
        lines.Add($"Request ID: {requestId}");
        lines.Add($"Details: {details}");
        lines.Add($"Underlying database cause: {underlying}");
        lines.Add("Technical details:");
        lines.Add(technical);
        lines.Add($"Search: {OperationFailureText.SearchGuidance}");
        return string.Join("\n", lines);
    }

    private static string StatusLine(int? statusCode, string? reason)
    {
        if (statusCode == null)
            return OperationFailureText.NotProvided;
        return string.IsNullOrWhiteSpace(reason) ? statusCode.Value.ToString() : $"{statusCode.Value} {reason}";
    }

    private static string StatusSentence(int? statusCode, string? reason, string? body)
    {
        var status = StatusLine(statusCode, reason);
        if (string.IsNullOrWhiteSpace(body))
            return $"The server returned HTTP {status} with an empty body.";
        return $"The server returned HTTP {status}.";
    }

    private static string? TryReadOperationOutcomeDiagnostics(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            var node = JsonNode.Parse(body);
            if (!string.Equals(node?["resourceType"]?.ToString(), "OperationOutcome", StringComparison.Ordinal))
                return null;

            if (node?["issue"] is not JsonArray issues)
                return null;

            foreach (var issue in issues)
            {
                var diagnostics = issue?["diagnostics"]?.ToString();
                if (!string.IsNullOrWhiteSpace(diagnostics))
                    return diagnostics;
                var text = issue?["details"]?["text"]?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }

        return null;
    }

    internal static string UnderlyingCause(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return OperationFailureText.NotProvided;

        var deeper = DeeperSqlMessage(text);
        if (deeper != null)
            return Cap(deeper, MaxDetailsChars);

        if (text.Contains("JpaSystemException", StringComparison.OrdinalIgnoreCase)
            || text.Contains("could not extract ResultSet", StringComparison.OrdinalIgnoreCase)
            || text.Contains("GenericJDBCException", StringComparison.OrdinalIgnoreCase))
            return OperationFailureText.UnderlyingCauseNotInResponse;

        return OperationFailureText.NotProvided;
    }

    private static string? DeeperSqlMessage(string text)
    {
        string[] markers = ["PSQLException:", "SQLException:", "SqlException:"];
        var best = -1;
        var markerLength = 0;
        foreach (var marker in markers)
        {
            var index = text.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index > best)
            {
                best = index;
                markerLength = marker.Length;
            }
        }

        if (best < 0)
            return null;

        var message = Collapse(text[(best + markerLength)..]);
        return string.IsNullOrWhiteSpace(message) ? null : message;
    }

    private static string? TraceIdFromTraceParent(string? traceParent)
    {
        if (string.IsNullOrWhiteSpace(traceParent))
            return null;

        var parts = traceParent.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[1] : null;
    }

    private static string? First(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string Provided(string? value) =>
        string.IsNullOrWhiteSpace(value) ? OperationFailureText.NotProvided : value.Trim();

    private static string Collapse(string value)
    {
        var collapsed = Regex.Replace(value, @"\s+", " ").Trim();
        return collapsed;
    }

    private static string Cap(string value, int max)
    {
        if (value.Length <= max)
            return value;
        return value[..max] + "…";
    }
}
