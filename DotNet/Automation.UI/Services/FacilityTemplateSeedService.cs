using Automation.UI.Models;
using Automation.UI.Services.Persistence;

namespace Automation.UI.Services;

/// <summary>
/// Seeds the system facility config plus Epic and Cerner convenience templates.
/// The system config points at the same query plan, normalization suite, and
/// organization resource map the pipeline used before facility templates existed.
/// </summary>
public sealed class FacilityTemplateSeedService(
    IFacilityTemplateStore store,
    ILogger<FacilityTemplateSeedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var template in BuildTemplates())
        {
            template.UpdatedAt = DateTimeOffset.UtcNow;
            await store.UpsertAsync(template, cancellationToken);
        }

        await store.SetDefaultAsync(FacilityTemplateCatalog.SystemDefaultId, cancellationToken);
        logger.LogInformation(
            "Seeded facility templates {SystemId}, {EpicId}, {CernerId}",
            FacilityTemplateCatalog.SystemDefaultId,
            FacilityTemplateCatalog.EpicId,
            FacilityTemplateCatalog.CernerId);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static IReadOnlyList<FacilityTemplate> BuildTemplates()
    {
        var allowedPatients = FacilityTemplateCatalog.SeededPatientConfigurationIds.ToList();
        return
        [
            new FacilityTemplate
            {
                Id = FacilityTemplateCatalog.SystemDefaultId,
                Name = "System Default",
                Description = "Shared facility config for system scenarios. Query plan, normalization suite, organization resource map, and vendor Epic match the configuration those scenarios used before facility templates.",
                IsSystem = true,
                IsDefault = true,
                VendorName = "Epic",
                QueryPlanTemplateId = FacilityTemplateCatalog.SystemQueryPlanId,
                NormalizationSuiteId = FacilityTemplateCatalog.SystemNormalizationSuiteId,
                OrganizationResourceMapTemplateId = FacilityTemplateCatalog.SystemOrganizationResourceMapId,
                EnableOrganizationLocationMapping = true,
                AllowedPatientConfigurationIds = allowedPatients,
                AllowPatientConfigurationsOutsideSet = true
            },
            new FacilityTemplate
            {
                Id = FacilityTemplateCatalog.EpicId,
                Name = "Epic",
                Description = "Epic convenience template. Hospital locations are identified by the Epic OID urn:oid:1.2.840.114350.1.13 plus RoleCode HOSP. Normalization removes Epic encounter, observation, and patient extensions. Taken from ehr-test3 patient 019eb19d-249b-7ea8-8ddf-0e82340c1776.",
                IsSystem = true,
                VendorName = "Epic",
                QueryPlanTemplateId = FacilityTemplateCatalog.EpicQueryPlanId,
                NormalizationSuiteId = FacilityTemplateCatalog.EpicNormalizationSuiteId,
                OrganizationResourceMapTemplateId = FacilityTemplateCatalog.EpicOrganizationResourceMapId,
                EnableOrganizationLocationMapping = true,
                AllowedPatientConfigurationIds = [.. allowedPatients],
                AllowPatientConfigurationsOutsideSet = true
            },
            new FacilityTemplate
            {
                Id = FacilityTemplateCatalog.CernerId,
                Name = "Cerner",
                Description = "Cerner convenience template. Hospital locations carry https://fhir.cerner.com/codeset/222 and RoleCode HOSP. Epic extension cleanup is not applied. Taken from ehr-test3 patient 019eb19f-a2a6-7de1-ae39-fa67552a7899.",
                IsSystem = true,
                VendorName = "Cerner",
                QueryPlanTemplateId = FacilityTemplateCatalog.CernerQueryPlanId,
                NormalizationSuiteId = FacilityTemplateCatalog.CernerNormalizationSuiteId,
                OrganizationResourceMapTemplateId = FacilityTemplateCatalog.CernerOrganizationResourceMapId,
                EnableOrganizationLocationMapping = true,
                AllowedPatientConfigurationIds = [.. allowedPatients],
                AllowPatientConfigurationsOutsideSet = true
            }
        ];
    }
}
