namespace Automation.UI.Models;

/// <summary>
/// Persisted so a later diagnostics export can show how suite sequences were
/// flattened into Normalization-service sequences, plus the Loki execution
/// evidence that suite-application validation used.
/// </summary>
public sealed class NormalizationEvidenceSnapshot
{
    public const string Domain = "normalizationEvidence";

    public string SuiteName { get; set; } = string.Empty;
    public int CollectedLineCount { get; set; }

    /// <summary>
    /// Raw Loki lines were left out so the snapshot stays under the Cosmos document cap.
    /// <see cref="CollectedLineCount"/> is still the number of lines validation used.
    /// </summary>
    public bool RawLinesOmitted { get; set; }

    /// <summary>Per-resource steps were rolled up by operation so the snapshot could be stored.</summary>
    public bool StepsCollapsed { get; set; }

    /// <summary>
    /// Additional snapshot documents named <c>normalizationEvidence-chunk-N</c> hold the raw lines
    /// and per-resource steps that did not fit in this document.
    /// </summary>
    public int EvidenceChunkCount { get; set; }

    public static string ChunkDomain(int oneBasedIndex) => $"{Domain}-chunk-{oneBasedIndex}";

    public List<string> SummaryLines { get; set; } = [];
    public List<NormalizationRuntimeSequenceStep> RuntimeSequences { get; set; } = [];
    public List<NormalizationSuiteSequenceStep> SuiteSequences { get; set; } = [];
    public List<NormalizationOperationConfigSnapshot> OperationConfigs { get; set; } = [];
    public List<NormalizationEvidenceStep> ParsedSteps { get; set; } = [];
}

public sealed class NormalizationRuntimeSequenceStep
{
    public string ResourceType { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
}

public sealed class NormalizationSuiteSequenceStep
{
    public string SequenceName { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public List<string> ResourceTypes { get; set; } = [];
}

public sealed class NormalizationOperationConfigSnapshot
{
    public string Name { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public List<string> ResourceTypes { get; set; } = [];
    public string? SourceFhirPath { get; set; }
    public string? TargetFhirPath { get; set; }
    public string? ConditionTargetFhirPath { get; set; }
    public string? ConditionTargetValue { get; set; }
    public List<string> Conditions { get; set; } = [];
    public string? CodeMapFhirPath { get; set; }
    public List<string> CodeSystemMaps { get; set; } = [];
    public List<string> ExtensionUrls { get; set; } = [];
}

public sealed class NormalizationEvidenceStep
{
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;

    /// <summary>
    /// How many resources this row represents. Zero means one resource, so older snapshots still count.
    /// </summary>
    public int Count { get; set; }
}

/// <summary>One Cosmos-sized piece of a normalization evidence snapshot.</summary>
public sealed class NormalizationEvidenceChunk
{
    public List<string> SummaryLines { get; set; } = [];
    public List<NormalizationEvidenceStep> ParsedSteps { get; set; } = [];
}
