using System.Security.Cryptography;
using System.Text;

namespace Automation.UI.Services;

public static class MetricsScenarioFingerprint
{
    public static string Compute(
        int patientCount,
        int seed,
        int resourcesMin,
        int resourcesMax,
        int? concurrency,
        string? benchmarkKey,
        IEnumerable<string>? measures,
        string? thetisGitSha,
        Guid? queryPlanId,
        Guid? normalizationSuiteId)
    {
        var payload = string.Join('|',
            patientCount.ToString(),
            seed.ToString(),
            resourcesMin.ToString(),
            resourcesMax.ToString(),
            concurrency?.ToString() ?? "default",
            benchmarkKey ?? "",
            string.Join(',', (measures ?? []).OrderBy(m => m, StringComparer.OrdinalIgnoreCase)),
            thetisGitSha ?? "",
            queryPlanId?.ToString("N") ?? "",
            normalizationSuiteId?.ToString("N") ?? "");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    public static int NextVersion(string? previousFingerprint, int previousVersion, string currentFingerprint)
    {
        if (string.IsNullOrWhiteSpace(previousFingerprint)
            || string.Equals(previousFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(1, previousVersion);
        }

        return Math.Max(1, previousVersion) + 1;
    }

    public static string Describe(
        int patientCount,
        int seed,
        int resourcesMin,
        int resourcesMax,
        int? concurrency)
    {
        var resources = resourcesMin == resourcesMax
            ? $"{resourcesMin} resources / patient"
            : $"{resourcesMin}–{resourcesMax} resources / patient";
        var parallel = concurrency is > 0 ? $" · {concurrency} queries at a time" : "";
        return $"{patientCount} patients · seed {seed} · {resources}{parallel}";
    }
}
