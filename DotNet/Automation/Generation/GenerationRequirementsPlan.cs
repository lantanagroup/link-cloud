namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Consumer-supplied generation requirements that describe data characteristics
/// the generated payload should include.
/// </summary>
public sealed class GenerationRequirementsPlan
{
    public string PlanName { get; set; } = string.Empty;
    public List<GenerationRequirement> Requirements { get; set; } = [];
}

public sealed class GenerationRequirement
{
    public string Name { get; set; } = string.Empty;
    public string RequirementType { get; set; } = string.Empty;
    public List<string> ResourceTypes { get; set; } = [];

    public string? SourceFhirPath { get; set; }
    public string? CodeMapFhirPath { get; set; }
    public List<string> ExtensionUrls { get; set; } = [];
    public List<GenerationRequirementCondition> Conditions { get; set; } = [];
    public List<GenerationRequirementCodeSystemMap> CodeSystemMaps { get; set; } = [];
}

public sealed class GenerationRequirementCondition
{
    public string FhirPathSource { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public object? Value { get; set; }
}

public sealed class GenerationRequirementCodeSystemMap
{
    public string SourceSystem { get; set; } = string.Empty;
    public Dictionary<string, string> SourceCodes { get; set; } = new(StringComparer.Ordinal);
}
