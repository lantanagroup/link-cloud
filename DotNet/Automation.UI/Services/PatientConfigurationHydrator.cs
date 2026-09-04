using Automation.UI.Services.Persistence;
using LantanaGroup.Automation.Generation;

namespace Automation.UI.Services;

public static class PatientConfigurationHydrator
{
    public static async Task<ResolvedRunOptions> HydrateAsync(
        ResolvedRunOptions options,
        IPatientConfigurationStore store,
        CancellationToken cancellationToken)
    {
        if (options.PatientCohorts.Count == 0)
            return options;

        var changed = false;
        foreach (var cohort in options.PatientCohorts)
        {
            if (!cohort.PatientConfigurationId.HasValue)
                continue;

            var config = await store.GetByIdAsync(cohort.PatientConfigurationId.Value, cancellationToken);
            if (config == null)
                continue;

            changed = true;
            cohort.Intent = PatientGenerationIntent.Merge(config.Intent, cohort.Intent);
            // Live reference: clinical profile, resource range, and stay timing come from
            // the configuration. Count stays on the cohort. Q/NQ is derived.
            cohort.EligibleClinicalScenarioIds = config.ClinicalScenarioIds.Take(1).ToList();
            if (config.ResourcesPerPatientMin > 0)
                cohort.ResourcesPerPatientMin = config.ResourcesPerPatientMin;
            if (config.ResourcesPerPatientMax >= cohort.ResourcesPerPatientMin)
                cohort.ResourcesPerPatientMax = config.ResourcesPerPatientMax;
            cohort.ScheduledInpatientPattern = config.ScheduledInpatientPattern
                ?? ScheduledStayWindow.DefaultPattern;
        }

        if (!changed)
            return options;

        var profiles = PatientCohortDefinition.ExpandProfiles(options.PatientCohorts, options.Seed);
        return options with { PatientProfiles = profiles, PatientCohorts = options.PatientCohorts };
    }
}
