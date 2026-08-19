using Automation.UI.Models;
using LantanaGroup.Automation.Generation;

namespace Automation.UI.Services;

public static class LivePatientPoolBuilder
{
    public static IReadOnlyList<LivePatientSeed> Build(
        IReadOnlyList<string> patientIds,
        IReadOnlyList<PatientProfile>? profilesAlignedToPatientIds,
        IEnumerable<string>? importedPatientIds,
        IReadOnlySet<string>? expectedInReportPatientIds = null)
    {
        var imported = (importedPatientIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.Ordinal);

        var seeds = new List<LivePatientSeed>();
        for (var i = 0; i < patientIds.Count; i++)
        {
            var id = patientIds[i];
            if (string.IsNullOrWhiteSpace(id))
                continue;

            id = id.Trim();
            PatientProfile? profile = null;
            if (profilesAlignedToPatientIds != null && i < profilesAlignedToPatientIds.Count)
                profile = profilesAlignedToPatientIds[i];

            var isImported = imported.Contains(id);
            seeds.Add(new LivePatientSeed
            {
                PatientId = id,
                Origin = isImported ? LivePatientOrigin.Import : LivePatientOrigin.Cohort,
                Pattern = isImported ? null : profile?.ScheduledInpatientPattern,
                ExpectedInReport = ResolveExpectedInReport(id, profile, expectedInReportPatientIds)
            });
        }

        foreach (var importedId in imported.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (seeds.Any(s => string.Equals(s.PatientId, importedId, StringComparison.Ordinal)))
                continue;

            seeds.Add(new LivePatientSeed
            {
                PatientId = importedId,
                Origin = LivePatientOrigin.Import,
                ExpectedInReport = expectedInReportPatientIds?.Contains(importedId)
            });
        }

        return seeds;
    }

    private static bool? ResolveExpectedInReport(
        string patientId,
        PatientProfile? profile,
        IReadOnlySet<string>? expectedInReportPatientIds)
    {
        if (expectedInReportPatientIds != null)
            return expectedInReportPatientIds.Contains(patientId);
        if (profile != null)
            return profile.IsExpectedInReportByCohortAndPattern();
        return null;
    }
}
