using System.Text.Json;
using System.Text.RegularExpressions;
using Automation.UI.Models;

namespace Automation.UI.Services;

/// <summary>
/// Parses Normalization-service <c>[NormalizationExecutionSummary]</c> log lines.
/// Step tokens are <c>{Sequence}:{OperationType}:{OperationName}:{Outcome}</c>;
/// the operation name may contain colons (CodeMap names often embed URLs).
/// </summary>
public static class NormalizationExecutionSummaryParser
{
    public const string Marker = "[NormalizationExecutionSummary]";

    public static IReadOnlyList<NormalizationEvidenceStep> ParseAll(IEnumerable<string> logLines)
    {
        var records = new List<NormalizationEvidenceStep>();
        foreach (var line in logLines)
            records.AddRange(ParseLine(line));
        return records;
    }

    public static IReadOnlyList<NormalizationEvidenceStep> ParseLine(string? rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
            return [];

        var trimmed = rawLine.Trim();
        string? resourceType = null;
        string? resourceId = null;
        string? steps = null;
        string? markerText = null;

        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;

                resourceType = GetString(root, "ResourceType") ?? GetString(root, "resourceType");
                resourceId = GetString(root, "ResourceId") ?? GetString(root, "resourceId");
                steps = GetString(root, "Steps") ?? GetString(root, "steps");
                markerText = GetString(root, "MessageTemplate")
                    ?? GetString(root, "RenderedMessage")
                    ?? GetString(root, "Message")
                    ?? GetString(root, "message")
                    ?? GetString(root, "Summary")
                    ?? GetString(root, "summary");
            }
            catch
            {
                // fall through to text parsing
            }
        }

        markerText ??= trimmed;
        if (!markerText.Contains(Marker, StringComparison.Ordinal))
            return [];

        if (string.IsNullOrWhiteSpace(resourceType) || string.IsNullOrWhiteSpace(resourceId) || string.IsNullOrWhiteSpace(steps))
        {
            var resourceTypeMatch = Regex.Match(markerText, @"(?:^|,\s*)ResourceType=(?<value>[^,]+)");
            var resourceIdMatch = Regex.Match(markerText, @"(?:^|,\s*)ResourceId=(?<value>[^,]+)");
            var stepsMatch = Regex.Match(markerText, @"(?:^|,\s*)Steps=\[(?<value>.*)\]\s*$");

            if (resourceTypeMatch.Success)
                resourceType = resourceTypeMatch.Groups["value"].Value.Trim();

            if (resourceIdMatch.Success)
                resourceId = resourceIdMatch.Groups["value"].Value.Trim();

            if (stepsMatch.Success)
                steps = stepsMatch.Groups["value"].Value;
        }

        if (string.IsNullOrWhiteSpace(resourceType) || string.IsNullOrWhiteSpace(resourceId) || string.IsNullOrWhiteSpace(steps))
            return [];

        var records = new List<NormalizationEvidenceStep>();
        foreach (var step in steps.Split(" | ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseStep(step, out var sequence, out var operationType, out var operationName, out var outcome))
                continue;

            records.Add(new NormalizationEvidenceStep
            {
                ResourceType = resourceType,
                ResourceId = resourceId,
                Sequence = sequence,
                OperationType = operationType,
                OperationName = operationName,
                Outcome = outcome
            });
        }

        return records;
    }

    public static bool TryParseStep(
        string step,
        out int sequence,
        out string operationType,
        out string operationName,
        out string outcome)
    {
        sequence = 0;
        operationType = string.Empty;
        operationName = string.Empty;
        outcome = string.Empty;

        if (string.IsNullOrWhiteSpace(step))
            return false;

        var first = step.IndexOf(':');
        if (first <= 0)
            return false;

        var second = step.IndexOf(':', first + 1);
        var last = step.LastIndexOf(':');
        if (second < 0 || last <= second)
            return false;

        if (!int.TryParse(step[..first], out sequence))
            return false;

        operationType = step[(first + 1)..second];
        operationName = step[(second + 1)..last];
        outcome = step[(last + 1)..];
        return operationType.Length > 0 && outcome.Length > 0;
    }

    /// <summary>
    /// Loki evidence is passed through <c>SanitizeForLog</c>, which replaces characters
    /// outside Latin-1 (including em-dash) with spaces. Generated operation names use
    /// "Suite — Op" so exact string equality misses real Success evidence.
    /// </summary>
    public static bool NamesMatch(string? left, string? right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
            return true;

        return string.Equals(NormalizeEvidenceName(left), NormalizeEvidenceName(right), StringComparison.Ordinal);
    }

    public static string NormalizeEvidenceName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var collapsed = new System.Text.StringBuilder(name.Length);
        var previousWasSpace = false;
        foreach (var ch in name)
        {
            var mapped = ch > 255 ? ' ' : ch;
            if (char.IsWhiteSpace(mapped))
            {
                if (previousWasSpace)
                    continue;
                collapsed.Append(' ');
                previousWasSpace = true;
                continue;
            }

            collapsed.Append(mapped);
            previousWasSpace = false;
        }

        return collapsed.ToString().Trim();
    }

    private static string? GetString(JsonElement node, string propertyName)
    {
        if (!node.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return null;

        return property.GetString();
    }
}
