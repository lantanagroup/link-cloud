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
            // Live reference for clinical shape. Resource range and stay on the
            // cohort are scenario overrides (mega/volume tests, scheduled-stay matrix);
            // fill them from the configuration only when the cohort left them unset.
            cohort.EligibleClinicalScenarioIds = config.ClinicalScenarioIds.Take(1).ToList();
            if (cohort.ResourcesPerPatientMin <= 0 && config.ResourcesPerPatientMin > 0)
                cohort.ResourcesPerPatientMin = config.ResourcesPerPatientMin;
            if (cohort.ResourcesPerPatientMax <= 0 && config.ResourcesPerPatientMax > 0)
                cohort.ResourcesPerPatientMax = config.ResourcesPerPatientMax;
            cohort.ScheduledInpatientPattern ??= config.ScheduledInpatientPattern
                ?? ScheduledStayWindow.DefaultPattern;
        }

        if (!changed)
            return options;

        var profiles = PatientCohortDefinition.ExpandProfiles(options.PatientCohorts, options.Seed);
        return options with { PatientProfiles = profiles, PatientCohorts = options.PatientCohorts };
    }
}
