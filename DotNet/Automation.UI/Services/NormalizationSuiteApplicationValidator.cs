using System.Text.Json;
using System.Text.RegularExpressions;

namespace Automation.UI.Services;

public sealed class NormalizationSuiteApplicationValidator
{
    private const int MaxErrors = 200;
    private readonly IAutomationOutput _output;

    public NormalizationSuiteApplicationValidator(IAutomationOutput output)
    {
        _output = output;
    }

    public Task ValidateAllAsync(
        IDictionary<string, object> internalAbsResources,
        NormalizationSuiteResolution suiteResolution,
        IReadOnlyList<string>? normalizationSummaryLogs = null)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var parsedResources = ParseInternalAbsResources(internalAbsResources, errors);
        var plannedOperations = BuildPlannedOperations(suiteResolution);
        var executionEvidence = ParseExecutionEvidence(normalizationSummaryLogs ?? [], warnings);

        if (plannedOperations.Count == 0)
            AddError(errors, "Resolved normalization suite has no operations to validate.");

        _output.WriteLine($"[Normalization Suite] Validating for suite '{suiteResolution.SuiteName}' with {suiteResolution.Sequences.Count} sequence(s) and {plannedOperations.Count} operation step(s).");

        foreach (var operation in plannedOperations)
        {
            var opResourceTypes = operation.Operation.ResourceTypes
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (opResourceTypes.Count == 0)
            {
                AddError(errors, $"Operation '{operation.Operation.Name}' has no resource types.");
                continue;
            }

            foreach (var resourceType in opResourceTypes)
            {
                var candidates = parsedResources
                    .Where(r => string.Equals(r.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (candidates.Count == 0)
                {
                    AddWarning(warnings, $"No ABS resources found for operation '{operation.Operation.Name}' targeting resource type '{resourceType}'. Skipping post-condition checks for this target.");
                    continue;
                }

                switch (operation.Operation.OperationType)
                {
                    case "RemoveExtensions":
                        ValidateRemoveExtensions(operation, resourceType, candidates, errors);
                        ValidateExecutionEvidence(operation, resourceType, candidates, executionEvidence, errors, warnings, evidenceOptional: true);
                        break;
                    default:
                        ValidateExecutionEvidence(operation, resourceType, candidates, executionEvidence, errors, warnings, evidenceOptional: false);
                        break;
                }
            }
        }

        if (errors.Count == 0)
        {
            _output.WriteLine("NORMALIZATION SUITE APPLICATION VALIDATION: Passed");
            foreach (var warning in warnings)
                _output.WriteLine($"  [WARN] {warning}");
            return Task.CompletedTask;
        }

        _output.WriteLine($"NORMALIZATION SUITE APPLICATION VALIDATION: Failed ({errors.Count} issue(s))");
        foreach (var error in errors)
            _output.WriteLine($"  - {error}");
        foreach (var warning in warnings)
            _output.WriteLine($"  [WARN] {warning}");

        throw new InvalidOperationException($"NORMALIZATION SUITE APPLICATION VALIDATION failed with {errors.Count} issue(s).");
    }

    private static List<PlannedOperationStep> BuildPlannedOperations(NormalizationSuiteResolution suiteResolution)
    {
        var planned = new List<PlannedOperationStep>();

        foreach (var sequence in suiteResolution.Sequences)
        {
            foreach (var op in sequence.Operations.OrderBy(o => o.Sequence))
            {
                planned.Add(new PlannedOperationStep(sequence.SequenceName, op.Sequence, op.Operation));
            }
        }

        foreach (var op in suiteResolution.StandaloneOperations)
            planned.Add(new PlannedOperationStep("(standalone)", 0, op));

        return planned;
    }

    private List<AbsResourceRecord> ParseInternalAbsResources(IDictionary<string, object> internalAbsResources, List<string> errors)
    {
        var parsed = new List<AbsResourceRecord>();

        foreach (var kvp in internalAbsResources)
        {
            if (!kvp.Key.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase) || kvp.Value is not string ndjson)
                continue;

            var lineNumber = 0;
            foreach (var line in ndjson.Split('\n'))
            {
                lineNumber++;
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    var root = doc.RootElement.Clone();
                    var resourceType = GetString(root, "resourceType") ?? string.Empty;
                    var id = GetString(root, "id") ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(resourceType))
                    {
                        AddError(errors, $"{kvp.Key}:{lineNumber} has no resourceType.");
                        continue;
                    }

                    parsed.Add(new AbsResourceRecord(kvp.Key, lineNumber, resourceType, id, root));
                }
                catch (Exception ex)
                {
                    AddError(errors, $"Failed to parse {kvp.Key}:{lineNumber}: {ex.Message}");
                }
            }
        }

        return parsed;
    }

    private void ValidateRemoveExtensions(
        PlannedOperationStep operation,
        string resourceType,
        IReadOnlyList<AbsResourceRecord> candidates,
        List<string> errors)
    {
        var extensionUrls = operation.Operation.ExtensionUrls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        if (extensionUrls.Count == 0)
        {
            AddError(errors, $"RemoveExtensions operation '{operation.Operation.Name}' has no configured extension URLs.");
            return;
        }

        var violations = 0;

        foreach (var record in candidates)
        {
            var topLevelExtensionUrls = GetTopLevelExtensionUrls(record.Resource);
            var forbidden = topLevelExtensionUrls
                .Where(extensionUrls.Contains)
                .ToList();

            if (forbidden.Count > 0)
            {
                violations += forbidden.Count;
                AddError(errors,
                    $"{operation.SequenceName}#{operation.Sequence}: '{operation.Operation.Name}' left forbidden top-level extension(s) on {resourceType}/{DisplayId(record.ResourceId)} in {record.SourceFile}: [{string.Join(", ", forbidden)}].");
            }
        }

        if (violations == 0)
        {
            _output.WriteLine($"[Normalization Suite] Verified RemoveExtensions '{operation.Operation.Name}' on {candidates.Count} {resourceType} resource(s): no forbidden extensions remained.");
        }
    }

    private void ValidateExecutionEvidence(
        PlannedOperationStep operation,
        string resourceType,
        IReadOnlyList<AbsResourceRecord> candidates,
        IReadOnlyList<ExecutionEvidenceRecord> evidence,
        List<string> errors,
        List<string> warnings,
        bool evidenceOptional)
    {
        var candidateCount = candidates.Count;
        if (candidateCount == 0)
        {
            AddWarning(warnings, $"No ABS candidates for {operation.SequenceName}#{operation.Sequence} '{operation.Operation.Name}' on '{resourceType}', skipping execution evidence requirement.");
            return;
        }

        var matches = evidence.Where(e =>
                string.Equals(e.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.OperationType, operation.Operation.OperationType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.OperationName, operation.Operation.Name, StringComparison.Ordinal)
                && e.Sequence == operation.Sequence)
            .ToList();

        if (matches.Count == 0)
        {
            if (!evidenceOptional)
            {
                AddError(errors, $"No normalization execution evidence found for {operation.SequenceName}#{operation.Sequence} '{operation.Operation.Name}' ({operation.Operation.OperationType}) on resource type '{resourceType}'.");
            }
            return;
        }

        _output.WriteLine($"[Normalization Suite] Evidence found for {operation.SequenceName}#{operation.Sequence} '{operation.Operation.Name}' on '{resourceType}': {matches.Count} record(s).");

        var candidateIds = candidates
            .Select(c => c.ResourceId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (candidateIds.Count > 0)
        {
            var matchedCandidateIds = matches
                .Select(m => m.ResourceId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);

            var missingCandidateEvidence = candidateIds
                .Where(id => !matchedCandidateIds.Contains(id))
                .Take(5)
                .ToList();

            if (missingCandidateEvidence.Count > 0)
            {
                if (!evidenceOptional)
                {
                    AddError(errors,
                        $"Execution evidence for '{operation.Operation.Name}' on '{resourceType}' did not include all ABS candidates (example missing IDs: {string.Join(", ", missingCandidateEvidence)}). ");
                }
                else
                {
                    AddWarning(warnings,
                        $"Execution evidence for '{operation.Operation.Name}' on '{resourceType}' did not include all ABS candidates (example missing IDs: {string.Join(", ", missingCandidateEvidence)}). ");
                }
            }
        }

        var hasSuccessOutcome = matches.Any(m => string.Equals(m.Outcome, "Success", StringComparison.OrdinalIgnoreCase));
        if (!evidenceOptional && !hasSuccessOutcome)
        {
            if (matches.All(m => string.Equals(m.Outcome, "NoAction", StringComparison.OrdinalIgnoreCase)))
                AddError(errors, $"Normalization execution evidence for '{operation.Operation.Name}' on '{resourceType}' only shows NoAction outcomes.");
            else if (matches.All(m => string.Equals(m.Outcome, "Failure", StringComparison.OrdinalIgnoreCase)))
                AddError(errors, $"Normalization execution evidence for '{operation.Operation.Name}' on '{resourceType}' only shows Failure outcomes.");
            else
                AddError(errors, $"Normalization execution evidence for '{operation.Operation.Name}' on '{resourceType}' did not show any Success outcomes.");
        }

        var outcomeSummary = string.Join(", ",
            matches
                .GroupBy(m => m.Outcome, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => $"{g.Key}={g.Count()}"));

        _output.WriteLine($"[Normalization Suite] Evidence outcomes for '{operation.Operation.Name}' on '{resourceType}': {outcomeSummary}.");
    }

    private IReadOnlyList<ExecutionEvidenceRecord> ParseExecutionEvidence(IReadOnlyList<string> logLines, List<string> warnings)
    {
        if (logLines.Count == 0)
        {
            AddWarning(warnings, "No normalization summary log evidence was provided for suite validation.");
            return [];
        }

        var records = new List<ExecutionEvidenceRecord>();
        foreach (var line in logLines)
        {
            var parsed = ParseSummaryLine(line);
            records.AddRange(parsed);
        }

        if (records.Count == 0)
            AddWarning(warnings, "Normalization summary logs were queried but no parsable [NormalizationExecutionSummary] records were found.");

        return records;
    }

    private static IReadOnlyList<ExecutionEvidenceRecord> ParseSummaryLine(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
            return [];

        var trimmed = rawLine.Trim();
        string? resourceType = null;
        string? resourceId = null;
        string? steps = null;
        string? markerText = null;

        if (trimmed.StartsWith("{", StringComparison.Ordinal))
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
                    ?? GetString(root, "message");

                markerText ??= GetString(root, "Summary") ?? GetString(root, "summary");
            }
            catch
            {
                // fall through to text parsing
            }
        }

        markerText ??= trimmed;
        if (!markerText.Contains("[NormalizationExecutionSummary]", StringComparison.Ordinal))
            return [];

        if (string.IsNullOrWhiteSpace(resourceType) || string.IsNullOrWhiteSpace(resourceId) || string.IsNullOrWhiteSpace(steps))
        {
            var summaryRegex = new Regex(@"ResourceType=(?<resourceType>[^,]+),\s*ResourceId=(?<resourceId>[^,]+),\s*Steps=\[(?<steps>.*)\]", RegexOptions.Compiled);
            var match = summaryRegex.Match(markerText);
            if (match.Success)
            {
                resourceType = match.Groups["resourceType"].Value.Trim();
                resourceId = match.Groups["resourceId"].Value.Trim();
                steps = match.Groups["steps"].Value;
            }
        }

        if (string.IsNullOrWhiteSpace(resourceType) || string.IsNullOrWhiteSpace(resourceId) || string.IsNullOrWhiteSpace(steps))
            return [];

        var records = new List<ExecutionEvidenceRecord>();
        foreach (var step in steps.Split(" | ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = step.Split(':', 4);
            if (parts.Length != 4)
                continue;

            if (!int.TryParse(parts[0], out var sequence))
                continue;

            records.Add(new ExecutionEvidenceRecord(
                ResourceType: resourceType,
                ResourceId: resourceId,
                Sequence: sequence,
                OperationType: parts[1],
                OperationName: parts[2],
                Outcome: parts[3]));
        }

        return records;
    }

    private static string DisplayId(string? id)
        => string.IsNullOrWhiteSpace(id) ? "(no-id)" : id;

    private static List<string> GetTopLevelExtensionUrls(JsonElement resource)
    {
        var urls = new List<string>();
        if (!resource.TryGetProperty("extension", out var extensionNode) || extensionNode.ValueKind != JsonValueKind.Array)
            return urls;

        foreach (var extension in extensionNode.EnumerateArray())
        {
            var url = GetString(extension, "url");
            if (!string.IsNullOrWhiteSpace(url))
                urls.Add(url);
        }

        return urls;
    }

    private static string? GetString(JsonElement node, string propertyName)
    {
        if (!node.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return null;

        return property.GetString();
    }

    private static void AddError(List<string> errors, string message)
    {
        if (errors.Count < MaxErrors)
            errors.Add(message);
    }

    private static void AddWarning(List<string> warnings, string message)
    {
        if (warnings.Count < MaxErrors)
            warnings.Add(message);
    }

    private sealed record AbsResourceRecord(string SourceFile, int LineNumber, string ResourceType, string ResourceId, JsonElement Resource);
    private sealed record PlannedOperationStep(string SequenceName, int Sequence, Automation.UI.Models.NormalizationOperationDefinition Operation);
    private sealed record ExecutionEvidenceRecord(string ResourceType, string ResourceId, int Sequence, string OperationType, string OperationName, string Outcome);
}
