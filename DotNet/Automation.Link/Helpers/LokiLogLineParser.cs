using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Parses Loki log lines from .NET Serilog Grafana-Loki JSON and Java logback text.
/// Serilog.Sinks.Grafana.Loki writes Exception as a string; services that call
/// Enrich.WithExceptionDetails() also add a structured ExceptionDetail object.
/// Java MeasureEval/Validation Loki appenders emit
/// <c>[thread] LEVEL logger message</c> (not JSON, and without %ex stack traces).
/// </summary>
public static class LokiLogLineParser
{
    private static readonly Regex ErrorLevelToken = new(
        @"\b(ERROR|FATAL|ERR)\b|""level""\s*:\s*""error""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AggregateCounter = new(
        @"\b(ERROR|WARNING|INFO|FATAL)=\d+\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Attempts to parse a structured JSON log line and extract both a concise
    /// summary and the full detail (including stack traces). The two parts are
    /// separated by <c>|||</c> so downstream consumers can display the summary
    /// as a header and the detail on expand.
    /// Falls back to the raw line if it's not JSON.
    /// </summary>
    public static string FormatLogLine(string logLine, int truncateLength = 0)
    {
        if (logLine.TrimStart().StartsWith('{'))
        {
            try
            {
                var obj = JObject.Parse(logLine);

                var level = obj["level"]?.ToString()
                    ?? obj["Level"]?.ToString()
                    ?? obj["@l"]?.ToString()
                    ?? "";
                var message = obj["Message"]?.ToString()
                    ?? obj["message"]?.ToString()
                    ?? obj["@m"]?.ToString()
                    ?? obj["@mt"]?.ToString()
                    ?? "";

                ExtractException(obj, out var exType, out var exMessage, out var exStackTrace, out var innerException);

                var summaryParts = new List<string>();
                if (!string.IsNullOrEmpty(level))
                    summaryParts.Add(level.ToUpperInvariant());
                if (!string.IsNullOrEmpty(message))
                    summaryParts.Add(message.Length > 200 ? message[..200] + "..." : message);
                if (!string.IsNullOrEmpty(exType))
                    summaryParts.Add($"{exType}: {(exMessage?.Length > 150 ? exMessage[..150] + "..." : exMessage)}");
                else if (!string.IsNullOrEmpty(exMessage))
                    summaryParts.Add(exMessage.Length > 150 ? exMessage[..150] + "..." : exMessage);

                var summary = summaryParts.Count > 0 ? string.Join(" | ", summaryParts) : "";

                var detailParts = new List<string>();
                if (!string.IsNullOrEmpty(message))
                    detailParts.Add($"Message: {message}");
                if (!string.IsNullOrEmpty(exType))
                    detailParts.Add($"Exception: {exType}");
                if (!string.IsNullOrEmpty(exMessage))
                    detailParts.Add($"Detail: {exMessage}");
                if (!string.IsNullOrEmpty(exStackTrace))
                    detailParts.Add($"Stack Trace:\n{exStackTrace}");
                if (!string.IsNullOrEmpty(innerException))
                    detailParts.Add($"Inner Exception:\n{innerException}");

                var detail = detailParts.Count > 0 ? string.Join("\n\n", detailParts) : "";

                if (!string.IsNullOrEmpty(summary))
                {
                    return string.IsNullOrEmpty(detail) || detail == summary
                        ? summary
                        : $"{summary}|||{detail}";
                }
            }
            catch
            {
                // Not valid JSON, fall through to raw truncation
            }
        }

        var maxLen = truncateLength > 0 ? truncateLength : 500;
        return logLine.Length > maxLen ? logLine[..maxLen] + "..." : logLine;
    }

    public static bool IsErrorLike(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        // Diagnostic rollup lines are telemetry, not errors.
        if (line.Contains("[DIAG]", StringComparison.OrdinalIgnoreCase))
            return false;

        // Aggregate counters like ERROR=140 are summaries, not individual error events.
        if (AggregateCounter.IsMatch(line))
            return false;

        return line.Contains("exception", StringComparison.OrdinalIgnoreCase)
            || line.Contains("unhandled", StringComparison.OrdinalIgnoreCase)
            || line.Contains("stacktrace", StringComparison.OrdinalIgnoreCase)
            || line.Contains("stack trace", StringComparison.OrdinalIgnoreCase)
            || line.Contains("caused by", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Failed to process event", StringComparison.OrdinalIgnoreCase)
            || line.Contains("System.", StringComparison.Ordinal)
            || line.Contains("java.", StringComparison.OrdinalIgnoreCase)
            || ErrorLevelToken.IsMatch(line);
    }

    private static void ExtractException(
        JObject obj,
        out string? exType,
        out string? exMessage,
        out string? exStackTrace,
        out string? innerException)
    {
        exType = null;
        exMessage = null;
        exStackTrace = null;
        innerException = null;

        ReadStructuredException(obj["ExceptionDetail"], ref exType, ref exMessage, ref exStackTrace, ref innerException);
        ReadStructuredException(obj["Exception"], ref exType, ref exMessage, ref exStackTrace, ref innerException);

        if (exStackTrace == null && obj["@x"] != null && obj["@x"].Type != JTokenType.Null)
        {
            var compact = obj["@x"].ToString();
            if (!string.IsNullOrWhiteSpace(compact))
            {
                exStackTrace = compact;
                if (exType == null)
                    SplitExceptionFirstLine(compact, out exType, out exMessage);
            }
        }
    }

    private static void ReadStructuredException(
        JToken? token,
        ref string? exType,
        ref string? exMessage,
        ref string? exStackTrace,
        ref string? innerException)
    {
        if (token == null || token.Type == JTokenType.Null)
            return;

        if (token is JObject exObj)
        {
            exType ??= exObj["Type"]?.ToString() ?? exObj["type"]?.ToString();
            exMessage ??= exObj["Message"]?.ToString() ?? exObj["message"]?.ToString();
            exStackTrace ??= exObj["StackTrace"]?.ToString()
                ?? exObj["StackTraceString"]?.ToString()
                ?? exObj["stackTrace"]?.ToString();
            innerException ??= FormatInnerException(exObj["InnerException"] ?? exObj["innerException"]);
            return;
        }

        var text = token.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return;

        exStackTrace ??= text;
        if (exType == null)
            SplitExceptionFirstLine(text, out exType, out exMessage);
    }

    private static string? FormatInnerException(JToken? inner)
    {
        if (inner == null || inner.Type == JTokenType.Null)
            return null;

        if (inner is JValue)
            return inner.ToString();

        return inner.ToString(Newtonsoft.Json.Formatting.Indented);
    }

    private static void SplitExceptionFirstLine(string text, out string? exType, out string? exMessage)
    {
        exType = null;
        exMessage = null;
        var firstLine = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstLine))
            return;

        var colon = firstLine.IndexOf(':');
        if (colon <= 0)
        {
            exType = firstLine.Trim();
            return;
        }

        exType = firstLine[..colon].Trim();
        exMessage = firstLine[(colon + 1)..].Trim();
    }
}
