using Thetis.Generation.Abstractions;

namespace LantanaGroup.Automation.Generation.Thetis;

/// <summary>
/// Deterministic per-patient IDs scoped by run tag. Patient/Encounter/Condition
/// usually come from explicit <c>resourceId</c> parameters; this generator covers
/// everything else the graph mints (observations, extra meds, …).
/// </summary>
internal sealed class RunTagResourceIdGenerator(string patientId) : IResourceIdGenerator
{
    private readonly Dictionary<string, int> _sequence = new(StringComparer.OrdinalIgnoreCase);

    public string Next(string resourceType, string? role = null)
    {
        var key = $"{resourceType}:{role ?? string.Empty}";
        _sequence.TryGetValue(key, out var n);
        n++;
        _sequence[key] = n;

        var abbrev = FhirBundleGenerator.AbbreviateResourceType(resourceType);
        var suffix = string.IsNullOrWhiteSpace(role) ? n.ToString("D3") : $"{role}-{n:D3}";
        return $"{patientId}-{abbrev}-{suffix}";
    }
}
