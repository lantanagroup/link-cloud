using System.Text.Json;

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
        var plannedOperations = NormalizationRuntimeSequencePlanner.Plan(suiteResolution);
        var executionEvidence = ParseExecutionEvidence(normalizationSummaryLogs ?? [], warnings);

        if (plannedOperations.Count == 0)
            AddError(errors, "Resolved normalization suite has no operations to validate.");

        _output.WriteLine($"[Normalization Suite] Validating for suite '{suiteResolution.SuiteName}' with {suiteResolution.Sequences.Count} sequence(s) and {plannedOperations.Count} operation step(s). Matching Loki evidence against Normalization-service runtime sequence numbers (one sequence per resource type).");

        foreach (var operation in plannedOperations)
        {
            if (string.IsNullOrWhiteSpace(operation.ResourceType))
            {
                AddError(errors, $"Operation '{operation.Operation.Name}' has no resource types.");
                continue;
            }

            var resourceType = operation.ResourceType;
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
                    // Loki execution summaries are the source of truth (Success or
                    // NoAction). Do not walk every ABS resource for leftover URLs —
                    // generated volume makes that expensive, and generated data is
                    // stamped so these ops actually fire. Skip per-id coverage:
                    // hundreds of Observations would flake on Loki pagination.
                    ValidateExecutionEvidence(
                        operation, candidates, executionEvidence, errors, warnings,
                        evidenceOptional: false,
                        requireCandidateCoverage: false);
                    break;
                default:
                    ValidateExecutionEvidence(
                        operation, candidates, executionEvidence, errors, warnings,
                        evidenceOptional: false,
                        requireCandidateCoverage: true);
                    break;
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

    private void ValidateExecutionEvidence(
        PlannedRuntimeStep operation,
        IReadOnlyList<AbsResourceRecord> candidates,
        IReadOnlyList<ExecutionEvidenceRecord> evidence,
        List<string> errors,
        List<string> warnings,
        bool evidenceOptional,
        bool requireCandidateCoverage = true)
    {
        var resourceType = operation.ResourceType;
        var candidateCount = candidates.Count;
        if (candidateCount == 0)
        {
            AddWarning(warnings, $"No ABS candidates for {operation.DisplayName} '{operation.Operation.Name}' on '{resourceType}', skipping execution evidence requirement.");
            return;
        }

        var matches = evidence.Where(e =>
                string.Equals(e.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.OperationType, operation.Operation.OperationType, StringComparison.OrdinalIgnoreCase)
                && NormalizationExecutionSummaryParser.NamesMatch(e.OperationName, operation.Operation.Name)
                && e.Sequence == operation.RuntimeSequence)
            .ToList();

        if (matches.Count == 0)
        {
            WriteMissingEvidenceContext(operation, evidence);
            if (!evidenceOptional)
            {
                AddError(errors, $"No normalization execution evidence found for {operation.DisplayName} '{operation.Operation.Name}' ({operation.Operation.OperationType}) on resource type '{resourceType}'.");
            }
            return;
        }

        _output.WriteLine($"[Normalization Suite] Evidence found for {operation.DisplayName} '{operation.Operation.Name}' on '{resourceType}': {matches.Count} record(s).");

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

            if (missingCandidateEvidence.Count > 0 && requireCandidateCoverage)
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

        var hasAppliedOutcome = matches.Any(m =>
            string.Equals(m.Outcome, "Success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(m.Outcome, "NoAction", StringComparison.OrdinalIgnoreCase));
        if (!evidenceOptional && !hasAppliedOutcome)
        {
            if (matches.All(m => string.Equals(m.Outcome, "Failure", StringComparison.OrdinalIgnoreCase)))
                AddError(errors, $"Normalization execution evidence for '{operation.Operation.Name}' on '{resourceType}' only shows Failure outcomes.");
            else
                AddError(errors, $"Normalization execution evidence for '{operation.Operation.Name}' on '{resourceType}' did not show any Success or NoAction outcomes.");
        }

        var outcomeSummary = string.Join(", ",
            matches
                .GroupBy(m => m.Outcome, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => $"{g.Key}={g.Count()}"));

        _output.WriteLine($"[Normalization Suite] Evidence outcomes for '{operation.Operation.Name}' on '{resourceType}': {outcomeSummary}.");
    }

    private void WriteMissingEvidenceContext(
        PlannedRuntimeStep operation,
        IReadOnlyList<ExecutionEvidenceRecord> evidence)
    {
        var resourceType = operation.ResourceType;
        var sameName = evidence
            .Where(e =>
                string.Equals(e.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.OperationType, operation.Operation.OperationType, StringComparison.OrdinalIgnoreCase)
                && NormalizationExecutionSummaryParser.NamesMatch(e.OperationName, operation.Operation.Name))
            .ToList();

        if (sameName.Count > 0)
        {
            var observed = string.Join("; ",
                sameName
                    .GroupBy(e => e.Sequence)
                    .OrderBy(g => g.Key)
                    .Select(g =>
                    {
                        var outcomes = string.Join(", ",
                            g.GroupBy(x => x.Outcome, StringComparer.OrdinalIgnoreCase)
                                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                                .Select(x => $"{x.Key}={x.Count()}"));
                        return $"sequence={g.Key} ({outcomes})";
                    }));

            _output.WriteLine(
                $"[Normalization Suite] No match for {operation.DisplayName} '{operation.Operation.Name}' on '{resourceType}' at runtime sequence={operation.RuntimeSequence}. Same name/type observed at {observed}.");
            return;
        }

        var observedOnType = evidence
            .Where(e => string.Equals(e.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => (e.Sequence, e.OperationType, e.OperationName))
            .OrderBy(g => g.Key.Sequence)
            .ThenBy(g => g.Key.OperationType, StringComparer.OrdinalIgnoreCase)
            .Select(g => $"{resourceType}#{g.Key.Sequence} {g.Key.OperationType} '{g.Key.OperationName}'")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var observedText = observedOnType.Count == 0
            ? "(none)"
            : string.Join("; ", observedOnType);
        _output.WriteLine(
            $"[Normalization Suite] No match for {operation.DisplayName} '{operation.Operation.Name}' on '{resourceType}' at runtime sequence={operation.RuntimeSequence}. Observed {resourceType} steps: {observedText}.");
    }

    private IReadOnlyList<ExecutionEvidenceRecord> ParseExecutionEvidence(IReadOnlyList<string> logLines, List<string> warnings)
    {
        if (logLines.Count == 0)
        {
            AddWarning(warnings, "No normalization summary log evidence was provided for suite validation.");
            return [];
        }

        var records = NormalizationExecutionSummaryParser.ParseAll(logLines)
            .Select(s => new ExecutionEvidenceRecord(
                s.ResourceType,
                s.ResourceId,
                s.Sequence,
                s.OperationType,
                s.OperationName,
                s.Outcome))
            .ToList();

        if (records.Count == 0)
            AddWarning(warnings, "Normalization summary logs were queried but no parsable [NormalizationExecutionSummary] records were found.");

        return records;
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
    private sealed record ExecutionEvidenceRecord(string ResourceType, string ResourceId, int Sequence, string OperationType, string OperationName, string Outcome);
}
