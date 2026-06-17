namespace LantanaGroup.Link.Automation.Link.Models;

public sealed class AutomationRunInputSnapshot
{
    public Guid RunId { get; init; }
    public Guid? ScenarioId { get; init; }
    public string? ScenarioName { get; init; }
    public string? RunConfigurationJson { get; init; }
    public IReadOnlyList<Guid> ImportedBundleIds { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class ImportedBundleSnapshot
{
    public Guid BundleId { get; init; }
    public string PatientId { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public string BundleJson { get; init; } = string.Empty;
    public long ByteCount { get; init; }
}
