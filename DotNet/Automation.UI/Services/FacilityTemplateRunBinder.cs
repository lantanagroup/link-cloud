using Automation.UI.Models;
using Automation.UI.Services.Persistence;

namespace Automation.UI.Services;

/// <summary>
/// Copies a facility template onto the run options so query plan, normalization,
/// organization resource map, and vendor come from the template and nowhere else.
/// </summary>
public static class FacilityTemplateRunBinder
{
    public static async Task<ResolvedRunOptions> ApplyAsync(
        ResolvedRunOptions options,
        IFacilityTemplateStore store,
        CancellationToken cancellationToken = default)
    {
        if (options.FacilityConfigurationMode != FacilityConfigurationMode.Facility)
            return options;

        if (!options.FacilityTemplateId.HasValue)
            throw new InvalidOperationException("Facility mode requires a facility template.");

        var template = await store.GetByIdAsync(options.FacilityTemplateId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Facility template '{options.FacilityTemplateId.Value}' was not found.");

        var patientError = FacilityConfigurationPolicy.ValidatePatientConfigurations(template, options.PatientCohorts);
        if (patientError != null)
            throw new InvalidOperationException(patientError);

        return options with
        {
            QueryPlanTemplateId = template.QueryPlanTemplateId,
            NormalizationSuiteId = template.NormalizationSuiteId,
            OrganizationResourceMapTemplateId = template.EnableOrganizationLocationMapping
                ? template.OrganizationResourceMapTemplateId
                : null,
            VendorName = string.IsNullOrWhiteSpace(template.VendorName) ? null : template.VendorName.Trim(),
            HonorExplicitFacilityPieces = true,
            AllowPatientConfigurationsOutsideSet = template.AllowPatientConfigurationsOutsideSet,
            AllowedPatientConfigurationIds = [.. template.AllowedPatientConfigurationIds]
        };
    }
}
