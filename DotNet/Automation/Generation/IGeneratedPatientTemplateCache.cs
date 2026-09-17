namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Cache for generated patient-bundle templates keyed by deterministic generation inputs.
/// Templates must carry a placeholder run tag so callers can materialize run-scoped IDs
/// without persisting per-run payloads.
/// </summary>
public interface IGeneratedPatientTemplateCache
{
    Task<GeneratedPatientTemplate?> GetAsync(string key, CancellationToken ct = default);
    Task StoreAsync(string key, GeneratedPatientTemplate template, CancellationToken ct = default);
}

public sealed record GeneratedPatientTemplate(string TemplateRunTag, IReadOnlyList<string> BundleJson);
