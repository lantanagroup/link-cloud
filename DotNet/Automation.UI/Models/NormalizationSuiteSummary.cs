namespace Automation.UI.Models;

/// <summary>Read-only view of a normalization suite stored in Automation.UI.</summary>
public sealed class NormalizationSuiteSummary
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsSystem { get; init; }
    public bool IsDefault { get; init; }
    public List<NormalizationSuiteSummarySequence> Sequences { get; init; } = [];
    public List<NormalizationSuiteSummaryOperation> Operations { get; init; } = [];
}

public sealed class NormalizationSuiteSummarySequence
{
    public string Name { get; init; } = string.Empty;
    public List<NormalizationSuiteSummaryOperation> Operations { get; init; } = [];
}

public sealed class NormalizationSuiteSummaryOperation
{
    public int Sequence { get; init; }
    public string Name { get; init; } = string.Empty;
    public string OperationType { get; init; } = string.Empty;
    public List<string> ResourceTypes { get; init; } = [];
    public bool Missing { get; init; }
}

/// <summary>Read-only view of an organization resource map stored in Automation.UI.</summary>
public sealed class OrganizationMapSummary
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsSystem { get; init; }
    public bool IsDefault { get; init; }
    public List<OrganizationMapConditionSummary> Conditions { get; init; } = [];
}

public sealed class OrganizationMapConditionSummary
{
    public int Priority { get; init; }
    public string FhirPath { get; init; } = string.Empty;
}

/// <summary>One configured operation shown in the run-details normalization panel.</summary>
public sealed class NormalizationOperationSummaryLine
{
    public string Name { get; init; } = string.Empty;
    public string OperationType { get; init; } = string.Empty;
    public List<string> ResourceTypes { get; init; } = [];
}

/// <summary>
/// Normalization suite, organization map, patient source, and execution evidence
/// for one run. Missing pieces use explicit states instead of errors.
/// </summary>
public sealed class NormalizationRunResults
{
    public string SuiteState { get; init; } = "not-configured";
    public Guid? SuiteId { get; init; }
    public string? SuiteName { get; init; }
    public NormalizationSuiteSummary? Suite { get; init; }

    public string OrganizationMapState { get; init; } = "not-configured";
    public Guid? OrganizationMapId { get; init; }
    public string? OrganizationMapName { get; init; }
    public OrganizationMapSummary? OrganizationMap { get; init; }

    /// <summary>generated, uploaded, mixed, or unknown.</summary>
    public string PatientSource { get; init; } = "unknown";
    public int GeneratedPatientCount { get; init; }
    public int UploadedPatientCount { get; init; }

    public string GeneratedConfirmationState { get; init; } = "not-applicable";
    public string GeneratedConfirmation { get; init; } = string.Empty;

    public string UploadedConfirmationState { get; init; } = "not-applicable";
    public string UploadedConfirmation { get; init; } = string.Empty;

    /// <summary>found, none, or pending.</summary>
    public string EvidenceState { get; init; } = "none";
    public string EvidenceMessage { get; init; } = string.Empty;
    public int EvidenceLineCount { get; init; }
    public int ParsedStepCount { get; init; }
    public List<string> EvidenceInventory { get; init; } = [];
    public List<NormalizationOperationSummaryLine> Operations { get; init; } = [];
}
