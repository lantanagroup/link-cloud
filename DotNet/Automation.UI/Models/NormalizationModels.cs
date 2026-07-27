namespace Automation.UI.Models;

/// <summary>
/// A reusable normalization operation definition stored locally in MongoDB.
/// This captures the operation type and its configuration parameters so they can
/// be composed into sequences and suites without hitting the remote Normalization API.
/// </summary>
public class NormalizationOperationDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string OperationType { get; set; } = string.Empty;

    /// <summary>FHIR resource types this operation applies to (e.g. "Location", "Encounter").</summary>
    public List<string> ResourceTypes { get; set; } = [];

    // --- CopyProperty fields ---
    public string? SourceFhirPath { get; set; }
    public string? TargetFhirPath { get; set; }

    // --- ConditionalTransform fields ---
    public string? ConditionTargetFhirPath { get; set; }
    public object? ConditionTargetValue { get; set; }
    public List<NormalizationCondition> Conditions { get; set; } = [];

    // --- CodeMap fields ---
    public string? CodeMapFhirPath { get; set; }
    public List<NormalizationCodeSystemMap> CodeSystemMaps { get; set; } = [];

    // --- RemoveExtensions fields ---
    public List<string> ExtensionUrls { get; set; } = [];

    // --- CopyLocationAliasToTypeIteratively fields ---
    public int MaxIterations { get; set; } = 15;
    public bool SplitOnComma { get; set; }

    // --- CopyLocation has no extra fields ---

    public bool IsSystem { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class NormalizationCondition
{
    public string FhirPathSource { get; set; } = string.Empty;
    public string Operator { get; set; } = "Equal";
    public object? Value { get; set; }
}

public class NormalizationCodeSystemMap
{
    public string SourceSystem { get; set; } = string.Empty;
    public string TargetSystem { get; set; } = string.Empty;
    public Dictionary<string, NormalizationCodeMapEntry> CodeMaps { get; set; } = new();
}

public class NormalizationCodeMapEntry
{
    public string Code { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
}

/// <summary>
/// An ordered series of normalization operations grouped by resource type.
/// </summary>
public class NormalizationSequenceDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Ordered list of operation IDs in this sequence.</summary>
    public List<NormalizationSequenceEntry> Entries { get; set; } = [];

    public bool IsSystem { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class NormalizationSequenceEntry
{
    /// <summary>Reference to a <see cref="NormalizationOperationDefinition.Id"/>.</summary>
    public Guid OperationId { get; set; }

    /// <summary>Order in the sequence (1-based).</summary>
    public int Sequence { get; set; }
}

/// <summary>
/// A bundle of operations and sequences that together represent a complete normalization
/// configuration selectable on a scenario.
/// </summary>
public class NormalizationSuiteDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Operation IDs included directly (not part of a sequence).</summary>
    public List<Guid> OperationIds { get; set; } = [];

    /// <summary>Sequence IDs included in this suite.</summary>
    public List<Guid> SequenceIds { get; set; } = [];

    public bool IsSystem { get; set; }
    public bool IsDefault { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
