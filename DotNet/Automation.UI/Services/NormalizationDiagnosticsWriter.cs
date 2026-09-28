using System.Globalization;
using System.Text;
using System.Text.Json;
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

        if (snapshot.RawLinesOmitted || snapshot.StepsCollapsed)
        {
            sb.AppendLine();
            sb.AppendLine("-- Snapshot size --");
            if (snapshot.RawLinesOmitted)
                sb.AppendLine($"  Raw log lines omitted ({snapshot.CollectedLineCount} collected). The Cosmos document cap cannot hold them.");
            if (snapshot.StepsCollapsed)
                sb.AppendLine("  Per-resource steps rolled up by operation so the snapshot could be stored.");
        }

        sb.AppendLine();
        sb.AppendLine($"-- Raw [NormalizationExecutionSummary] lines ({snapshot.SummaryLines.Count}) --");
        if (snapshot.SummaryLines.Count == 0 && snapshot.RawLinesOmitted)
            sb.AppendLine($"  ({snapshot.CollectedLineCount} collected; raw lines omitted from this document)");
        else if (snapshot.SummaryLines.Count == 0 && snapshot.EvidenceChunkCount > 0)
            sb.AppendLine($"  (raw lines are stored in {snapshot.EvidenceChunkCount} snapshot chunk(s))");
        else if (snapshot.SummaryLines.Count == 0)
            sb.AppendLine("  (none collected)");
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
                            .Select(g => $"{g.Key}={g.Sum(StepWeight)}"));
                    return (key.ResourceType, key.Sequence, Line:
                        $"{key.ResourceType}#{key.Sequence} {key.OperationType} '{key.OperationName}': {outcomes}");
                })
            .OrderBy(x => x.ResourceType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Sequence)
            .Select(x => x.Line)
            .ToList();
    }

    /// <summary>
    /// Cosmos DB for MongoDB rejects documents over about 2 MB with HTTP 413.
    /// A mega-patient evidence snapshot (one Loki line and one step per resource)
    /// crosses that inline cap. <see cref="PlanPersistence"/> keeps operation counts
    /// on the header and stores the rest as more snapshot documents. This copy is
    /// the single-document fallback: raw lines dropped, steps rolled up, counts kept.
    /// </summary>
    internal const int CosmosSafeInlineBytes = 1_500_000;

    internal static int SerializedUtf8Bytes(NormalizationEvidenceSnapshot snapshot)
        => Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(snapshot));

    internal static NormalizationEvidenceSnapshot FitToCosmosInlineLimit(
        NormalizationEvidenceSnapshot source,
        int maxUtf8Bytes = CosmosSafeInlineBytes)
    {
        if (maxUtf8Bytes <= 0 || SerializedUtf8Bytes(source) <= maxUtf8Bytes)
            return source;

        var fitted = Copy(source);
        fitted.SummaryLines = [];
        fitted.RawLinesOmitted = true;
        if (SerializedUtf8Bytes(fitted) <= maxUtf8Bytes)
            return fitted;

        fitted.ParsedSteps = Collapse(source.ParsedSteps);
        fitted.StepsCollapsed = true;
        return fitted;
    }

    internal sealed record PersistencePlan(
        NormalizationEvidenceSnapshot Header,
        IReadOnlyList<NormalizationEvidenceChunk> Chunks);

    /// <summary>
    /// Keeps the operation counts in the header document. Raw lines and per-resource
    /// steps that do not fit move into additional snapshot documents in the same store.
    /// </summary>
    internal static PersistencePlan PlanPersistence(
        NormalizationEvidenceSnapshot source,
        int maxUtf8Bytes = CosmosSafeInlineBytes)
    {
        if (maxUtf8Bytes <= 0 || SerializedUtf8Bytes(source) <= maxUtf8Bytes)
        {
            var inline = Copy(source);
            inline.EvidenceChunkCount = 0;
            return new PersistencePlan(inline, []);
        }

        var chunks = PackChunks(source.SummaryLines, source.ParsedSteps, maxUtf8Bytes);
        var header = Copy(source);
        header.SummaryLines = [];
        header.RawLinesOmitted = false;
        header.ParsedSteps = Collapse(source.ParsedSteps);
        header.StepsCollapsed = source.ParsedSteps.Count > 0;
        header.EvidenceChunkCount = chunks.Count;
        return new PersistencePlan(header, chunks);
    }

    internal static NormalizationEvidenceSnapshot Assemble(
        NormalizationEvidenceSnapshot header,
        IReadOnlyList<NormalizationEvidenceChunk> chunks)
    {
        if (header.EvidenceChunkCount == 0 || chunks.Count == 0)
            return header;

        var assembled = Copy(header);
        var lines = chunks.SelectMany(c => c.SummaryLines).ToList();
        if (lines.Count > 0)
            assembled.SummaryLines = lines;

        // A missing chunk would make a partial step list look like the full inventory
        // and hide the collapsed counts that were stored on the header.
        var steps = chunks.SelectMany(c => c.ParsedSteps).ToList();
        if (chunks.Count >= header.EvidenceChunkCount && steps.Count > 0)
        {
            assembled.ParsedSteps = steps;
            assembled.StepsCollapsed = false;
        }

        return assembled;
    }

    private static List<NormalizationEvidenceChunk> PackChunks(
        IReadOnlyList<string> lines,
        IReadOnlyList<NormalizationEvidenceStep> steps,
        int maxUtf8Bytes)
    {
        var chunks = new List<NormalizationEvidenceChunk>();
        var current = new NormalizationEvidenceChunk();

        void Flush()
        {
            if (current.SummaryLines.Count == 0 && current.ParsedSteps.Count == 0)
                return;
            chunks.Add(current);
            current = new NormalizationEvidenceChunk();
        }

        foreach (var line in lines)
        {
            var stored = line;
            var candidate = CopyChunk(current);
            candidate.SummaryLines.Add(stored);
            if (SerializedChunkBytes(candidate) > maxUtf8Bytes && (current.SummaryLines.Count > 0 || current.ParsedSteps.Count > 0))
            {
                Flush();
                candidate = new NormalizationEvidenceChunk { SummaryLines = [stored] };
            }

            if (SerializedChunkBytes(candidate) > maxUtf8Bytes)
                candidate = FitSingleLine(stored, maxUtf8Bytes);

            current = candidate;
        }

        foreach (var step in steps)
        {
            var candidate = CopyChunk(current);
            candidate.ParsedSteps.Add(step);
            if (SerializedChunkBytes(candidate) > maxUtf8Bytes && (current.SummaryLines.Count > 0 || current.ParsedSteps.Count > 0))
            {
                Flush();
                candidate = new NormalizationEvidenceChunk { ParsedSteps = [step] };
            }

            current = candidate;
        }

        Flush();
        return chunks;
    }

    private static NormalizationEvidenceChunk FitSingleLine(string line, int maxUtf8Bytes)
    {
        const string suffix = " [truncated: exceeded snapshot chunk budget]";
        var stored = line;
        var candidate = new NormalizationEvidenceChunk { SummaryLines = [stored] };
        while (SerializedChunkBytes(candidate) > maxUtf8Bytes && stored.Length > suffix.Length + 32)
        {
            var keepChars = Math.Max(32, (stored.Length - suffix.Length) / 2);
            stored = stored[..keepChars] + suffix;
            candidate = new NormalizationEvidenceChunk { SummaryLines = [stored] };
        }

        return candidate;
    }

    private static NormalizationEvidenceChunk CopyChunk(NormalizationEvidenceChunk source)
        => new()
        {
            SummaryLines = [.. source.SummaryLines],
            ParsedSteps = [.. source.ParsedSteps]
        };

    private static int SerializedChunkBytes(NormalizationEvidenceChunk chunk)
        => Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(chunk));

    internal static bool IsOversizedWrite(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            var text = current.Message;
            if (text.Contains("RequestEntityTooLarge", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Request size is too large", StringComparison.OrdinalIgnoreCase)
                || text.Contains("413", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int StepWeight(NormalizationEvidenceStep step) => step.Count > 0 ? step.Count : 1;

    private static NormalizationEvidenceSnapshot Copy(NormalizationEvidenceSnapshot source)
        => new()
        {
            SuiteName = source.SuiteName,
            CollectedLineCount = source.CollectedLineCount,
            RawLinesOmitted = source.RawLinesOmitted,
            StepsCollapsed = source.StepsCollapsed,
            EvidenceChunkCount = source.EvidenceChunkCount,
            SummaryLines = [.. source.SummaryLines],
            RuntimeSequences = [.. source.RuntimeSequences],
            SuiteSequences = [.. source.SuiteSequences],
            OperationConfigs = [.. source.OperationConfigs],
            ParsedSteps = [.. source.ParsedSteps]
        };

    private static List<NormalizationEvidenceStep> Collapse(IReadOnlyList<NormalizationEvidenceStep> steps)
    {
        return steps
            .GroupBy(
                s => $"{s.ResourceType}\u001f{s.Sequence}\u001f{s.OperationType}\u001f{s.OperationName}\u001f{s.Outcome}",
                StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First();
                return new NormalizationEvidenceStep
                {
                    ResourceType = first.ResourceType,
                    ResourceId = string.Empty,
                    Sequence = first.Sequence,
                    OperationType = first.OperationType,
                    OperationName = first.OperationName,
                    Outcome = first.Outcome,
                    Count = g.Sum(StepWeight)
                };
            })
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
