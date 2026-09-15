namespace Automation.UI.Models;

public sealed class BundleConfigFingerprint
{
    public Dictionary<string, int> ResourceCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<LocationIdentifierHint> LocationIdentifiers { get; set; } = [];
    public List<LocationTypeHint> LocationTypes { get; set; } = [];
    public List<string> LocationAliases { get; set; } = [];
    public List<ExtensionHint> Extensions { get; set; } = [];
    public List<CodingHint> Codings { get; set; } = [];
    public int LocationCount { get; set; }
    public int PatientCount { get; set; }
    public int LocationsWithoutIdentifier { get; set; }
}

public sealed class LocationIdentifierHint
{
    public string System { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class LocationTypeHint
{
    public string System { get; set; } = "";
    public string Code { get; set; } = "";
}

public sealed class ExtensionHint
{
    public string Url { get; set; } = "";
    public string ResourceType { get; set; } = "";
}

public sealed class CodingHint
{
    public string ResourceType { get; set; } = "";
    public string Path { get; set; } = "";
    public string System { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Display { get; set; }
}

public sealed class ReuseCandidate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public double Score { get; set; }
    public string Recommendation { get; set; } = "";
    public string Reason { get; set; } = "";
}

public sealed class GeneratedOrmProposal
{
    public string SuggestedName { get; set; } = "";
    public string SuggestedDescription { get; set; } = "";
    public List<OrganizationResourceMapCondition> Conditions { get; set; } = [];
    public List<ReuseCandidate> Reuse { get; set; } = [];
    public List<string> Notes { get; set; } = [];
}

public sealed class GeneratedNormalizationOperationProposal
{
    public string OperationType { get; set; } = "";
    public string SuggestedName { get; set; } = "";
    public string SuggestedDescription { get; set; } = "";
    public List<string> ResourceTypes { get; set; } = [];
    public string? SourceFhirPath { get; set; }
    public string? TargetFhirPath { get; set; }
    public string? ConditionTargetFhirPath { get; set; }
    public object? ConditionTargetValue { get; set; }
    public List<NormalizationCondition> Conditions { get; set; } = [];
    public string? CodeMapFhirPath { get; set; }
    public List<NormalizationCodeSystemMap> CodeSystemMaps { get; set; } = [];
    public List<string> ExtensionUrls { get; set; } = [];
    public int MaxIterations { get; set; } = 15;
    public bool SplitOnComma { get; set; }
    public Guid? ReuseOperationId { get; set; }
    public string? ReuseOperationName { get; set; }
}

public sealed class GeneratedNormalizationProposal
{
    public string SuggestedSuiteName { get; set; } = "";
    public string SuggestedSuiteDescription { get; set; } = "";
    public string SuggestedSequenceName { get; set; } = "";
    public List<GeneratedNormalizationOperationProposal> Operations { get; set; } = [];
    public List<ReuseCandidate> Reuse { get; set; } = [];
    public List<string> Notes { get; set; } = [];
}

public sealed class BundleConfigurationProposal
{
    public BundleConfigFingerprint Fingerprint { get; set; } = new();
    public GeneratedOrmProposal Orm { get; set; } = new();
    public GeneratedNormalizationProposal Normalization { get; set; } = new();
    public List<string> Summary { get; set; } = [];
    public bool CombinedWithPrior { get; set; }
    public int SourceCount { get; set; } = 1;
    public Guid? RefinedOrmId { get; set; }
    public Guid? RefinedSuiteId { get; set; }
}

public sealed class AnalyzeBundleSource
{
    public string? Source { get; set; }
    public string? PatientId { get; set; }
    public string? BundleJson { get; set; }
    public Guid? UploadedBundleId { get; set; }
}

public sealed class AnalyzeBundleConfigurationRequest
{
    public string? Source { get; set; }
    public string? PatientId { get; set; }
    public string? BundleJson { get; set; }
    public Guid? UploadedBundleId { get; set; }
    public List<AnalyzeBundleSource>? Sources { get; set; }
    public BundleConfigFingerprint? PriorFingerprint { get; set; }
    public Guid? RefineOrmId { get; set; }
    public Guid? RefineSuiteId { get; set; }
}

public sealed class ApplyGeneratedOrmRequest
{
    public GeneratedOrmProposal Proposal { get; set; } = new();
    public Guid? UpdateExistingId { get; set; }
}

public sealed class ApplyGeneratedNormalizationRequest
{
    public GeneratedNormalizationProposal Proposal { get; set; } = new();
    public Guid? UpdateExistingSuiteId { get; set; }
}
