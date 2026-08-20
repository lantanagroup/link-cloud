using System.Globalization;
using System.Text;
using Automation.UI.Models;

namespace Automation.UI.Services;

/// <summary>
/// Builds and formats the persisted normalization-evidence snapshot used by
/// run logs and the diagnostics ZIP export.
/// </summary>
internal static class NormalizationDiagnosticsWriter
{
    public static NormalizationEvidenceSnapshot Build(
        NormalizationSuiteResolution resolution,
        IReadOnlyList<NormalizationRuntimeSequenceStep> runtimeSequences,
        IReadOnlyList<string> summaryLines)
    {
        var parsed = NormalizationExecutionSummaryParser.ParseAll(summaryLines);
        return new NormalizationEvidenceSnapshot
        {
            SuiteName = resolution.SuiteName,
            CollectedLineCount = summaryLines.Count,
            SummaryLines = [.. summaryLines],
            RuntimeSequences = [.. runtimeSequences],
            SuiteSequences = BuildSuiteSequences(resolution),
            OperationConfigs = BuildOperationConfigs(resolution),
            ParsedSteps = [.. parsed]
        };
    }

    public static void WriteInventory(IAutomationOutput output, NormalizationEvidenceSnapshot snapshot)
    {
        output.WriteLine(
            $"[Normalization Suite] Parsed {snapshot.ParsedSteps.Count} execution step(s) from {snapshot.CollectedLineCount} summary line(s).");

        if (snapshot.RuntimeSequences.Count > 0)
        {
            output.WriteLine("[Normalization Suite] Runtime sequences applied by the Normalization service (per resource type):");
            foreach (var step in snapshot.RuntimeSequences
                         .OrderBy(s => s.ResourceType, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(s => s.Sequence))
            {
                output.WriteLine(
                    $"[Normalization Suite]   [runtime] {step.ResourceType}#{step.Sequence} {step.OperationType} '{step.OperationName}'");
            }
        }

        foreach (var line in FormatEvidenceInventory(snapshot.ParsedSteps))
            output.WriteLine($"[Normalization Suite] {line}");
    }

    public static string FormatExportAppendix(NormalizationEvidenceSnapshot? snapshot)
    {
        var sb = new StringBuilder();
        if (snapshot == null)
        {
            sb.AppendLine();
            sb.AppendLine("-- Normalization evidence snapshot --");
            sb.AppendLine("(not persisted for this run; re-run after this build to capture suite vs runtime sequences and Loki execution summaries)");
            return sb.ToString();
        }

        sb.AppendLine();
        sb.AppendLine($"-- Suite '{snapshot.SuiteName}' sequences (Automation numbering) --");
        if (snapshot.SuiteSequences.Count == 0)
        {
            sb.AppendLine("  (none)");
        }
        else
        {
            string? current = null;
            foreach (var step in snapshot.SuiteSequences)
            {
                if (!string.Equals(current, step.SequenceName, StringComparison.Ordinal))
                {
                    current = step.SequenceName;
                    sb.AppendLine($"  {step.SequenceName}");
                }

                sb.AppendLine(
                    $"    Sequence={step.Sequence} {step.OperationType} '{step.OperationName}' [{string.Join(", ", step.ResourceTypes)}]");
            }
        }

        sb.AppendLine();
        sb.AppendLine("-- Runtime sequences (Normalization service, per resource type) --");
        if (snapshot.RuntimeSequences.Count == 0)
        {
            sb.AppendLine("  (none recorded)");
        }
        else
        {
            string? current = null;
            foreach (var step in snapshot.RuntimeSequences
                         .OrderBy(s => s.ResourceType, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(s => s.Sequence))
            {
                if (!string.Equals(current, step.ResourceType, StringComparison.OrdinalIgnoreCase))
                {
                    current = step.ResourceType;
                    sb.AppendLine($"  {step.ResourceType}");
                }

                sb.AppendLine($"    Sequence={step.Sequence} {step.OperationType} '{step.OperationName}'");
            }
        }

        if (snapshot.OperationConfigs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("-- Operation configurations --");
            foreach (var op in snapshot.OperationConfigs)
            {
                sb.AppendLine($"  {op.OperationType} '{op.Name}' [{string.Join(", ", op.ResourceTypes)}]");
                foreach (var detail in FormatOperationConfigDetails(op))
                    sb.AppendLine($"    {detail}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("-- Execution evidence inventory --");
        var inventory = FormatEvidenceInventory(snapshot.ParsedSteps);
        if (inventory.Count == 0)
            sb.AppendLine("  (no parsable [NormalizationExecutionSummary] steps)");
        else
        {
            foreach (var line in inventory)
                sb.AppendLine($"  {line}");
        }

        sb.AppendLine();
        sb.AppendLine($"-- Raw [NormalizationExecutionSummary] lines ({snapshot.SummaryLines.Count}) --");
        if (snapshot.SummaryLines.Count == 0)
        {
            sb.AppendLine("  (none collected)");
        }
        else
        {
            foreach (var line in snapshot.SummaryLines)
                sb.AppendLine(line);
        }

        return sb.ToString();
    }

    internal static IReadOnlyList<string> FormatEvidenceInventory(IEnumerable<NormalizationEvidenceStep> steps)
    {
        return steps
            .GroupBy(
                s => (s.ResourceType, s.Sequence, s.OperationType, s.OperationName),
                (key, group) =>
                {
                    var outcomes = string.Join(", ",
                        group
                            .GroupBy(g => g.Outcome, StringComparer.OrdinalIgnoreCase)
                            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                            .Select(g => $"{g.Key}={g.Count()}"));
                    return (key.ResourceType, key.Sequence, Line:
                        $"{key.ResourceType}#{key.Sequence} {key.OperationType} '{key.OperationName}': {outcomes}");
                })
            .OrderBy(x => x.ResourceType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Sequence)
            .Select(x => x.Line)
            .ToList();
    }

    private static List<NormalizationSuiteSequenceStep> BuildSuiteSequences(NormalizationSuiteResolution resolution)
    {
        var steps = new List<NormalizationSuiteSequenceStep>();
        foreach (var sequence in resolution.Sequences)
        {
            foreach (var op in sequence.Operations.OrderBy(o => o.Sequence))
            {
                steps.Add(new NormalizationSuiteSequenceStep
                {
                    SequenceName = sequence.SequenceName,
                    Sequence = op.Sequence,
                    OperationType = op.Operation.OperationType,
                    OperationName = op.Operation.Name,
                    ResourceTypes = [.. op.Operation.ResourceTypes]
                });
            }
        }

        foreach (var op in resolution.StandaloneOperations)
        {
            steps.Add(new NormalizationSuiteSequenceStep
            {
                SequenceName = "(standalone)",
                Sequence = 0,
                OperationType = op.OperationType,
                OperationName = op.Name,
                ResourceTypes = [.. op.ResourceTypes]
            });
        }

        return steps;
    }

    private static List<NormalizationOperationConfigSnapshot> BuildOperationConfigs(NormalizationSuiteResolution resolution)
    {
        return resolution.Operations
            .GroupBy(o => o.Id)
            .Select(g => g.First())
            .Select(op => new NormalizationOperationConfigSnapshot
            {
                Name = op.Name,
                OperationType = op.OperationType,
                ResourceTypes = [.. op.ResourceTypes],
                SourceFhirPath = op.SourceFhirPath,
                TargetFhirPath = op.TargetFhirPath,
                ConditionTargetFhirPath = op.ConditionTargetFhirPath,
                ConditionTargetValue = Convert.ToString(op.ConditionTargetValue, CultureInfo.InvariantCulture),
                Conditions = op.Conditions
                    .Select(c => $"{c.FhirPathSource} {c.Operator} {Convert.ToString(c.Value, CultureInfo.InvariantCulture)}")
                    .ToList(),
                CodeMapFhirPath = op.CodeMapFhirPath,
                CodeSystemMaps = op.CodeSystemMaps
                    .Select(m => $"{m.SourceSystem} -> {m.TargetSystem} ({m.CodeMaps.Count} code(s))")
                    .ToList(),
                ExtensionUrls = [.. op.ExtensionUrls]
            })
            .ToList();
    }

    private static IEnumerable<string> FormatOperationConfigDetails(NormalizationOperationConfigSnapshot op)
    {
        if (!string.IsNullOrWhiteSpace(op.SourceFhirPath) || !string.IsNullOrWhiteSpace(op.TargetFhirPath))
            yield return $"Copy {op.SourceFhirPath} -> {op.TargetFhirPath}";

        if (!string.IsNullOrWhiteSpace(op.ConditionTargetFhirPath))
            yield return $"Set {op.ConditionTargetFhirPath} = {op.ConditionTargetValue}";

        foreach (var condition in op.Conditions)
            yield return $"When {condition}";

        if (!string.IsNullOrWhiteSpace(op.CodeMapFhirPath))
            yield return $"CodeMap path {op.CodeMapFhirPath}";

        foreach (var map in op.CodeSystemMaps)
            yield return map;

        if (op.ExtensionUrls.Count > 0)
            yield return $"Extension URLs: {string.Join(", ", op.ExtensionUrls)}";
    }
}
