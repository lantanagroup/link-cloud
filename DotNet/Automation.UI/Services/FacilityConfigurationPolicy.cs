using Automation.UI.Models;

namespace Automation.UI.Services;

/// <summary>
/// Checks a scenario's cohorts against the patient configurations a facility template allows.
/// </summary>
public static class FacilityConfigurationPolicy
{
    public static string? ValidatePatientConfigurations(
        FacilityTemplate template,
        IEnumerable<PatientCohortDefinition>? cohorts)
    {
        if (template.AllowPatientConfigurationsOutsideSet)
            return null;

        var allowed = template.AllowedPatientConfigurationIds.ToHashSet();
        foreach (var cohort in cohorts ?? [])
        {
            if (!cohort.PatientConfigurationId.HasValue)
            {
                return $"Facility template '{template.Name}' does not allow patient configurations outside its set, and a cohort has none selected.";
            }

            if (!allowed.Contains(cohort.PatientConfigurationId.Value))
            {
                return $"Patient configuration '{cohort.PatientConfigurationId}' is not available on facility template '{template.Name}'.";
            }
        }

        return null;
    }
}
