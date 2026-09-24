using Automation.UI.Models;
using Automation.UI.Services.Persistence;

namespace Automation.UI.Services;

public sealed class OrganizationResourceMapTemplateSeedService : IHostedService
{
    private static readonly Guid SystemDefaultTemplateId = FacilityTemplateCatalog.SystemOrganizationResourceMapId;

    private readonly IOrganizationResourceMapTemplateStore _store;
    private readonly ILogger<OrganizationResourceMapTemplateSeedService> _logger;

    public OrganizationResourceMapTemplateSeedService(
        IOrganizationResourceMapTemplateStore store,
        ILogger<OrganizationResourceMapTemplateSeedService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var template = new OrganizationResourceMapTemplate
        {
            Id = SystemDefaultTemplateId,
            Name = "System Default",
            Description = "Automation default org-location mapping for generated synthetic data.",
            IsSystem = true,
            IsDefault = true,
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = "identifier.where(system='http://example.org/fhir/sid/location').exists() or type.coding.where(system='https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html').exists()",
                    Priority = 1
                }
            ],
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _store.UpsertAsync(template, cancellationToken);

        await _store.UpsertAsync(new OrganizationResourceMapTemplate
        {
            Id = FacilityTemplateCatalog.EpicOrganizationResourceMapId,
            Name = "Epic",
            Description = "Matches Epic hospital locations: identifier system urn:oid:1.2.840.114350.1.13 and RoleCode HOSP. Rooms and floors in the ehr-test3 Epic bundle carry the OID without HOSP.",
            IsSystem = true,
            IsDefault = false,
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = FacilityTemplateCatalog.EpicOrgLocationFhirPath,
                    Priority = 1
                }
            ],
            UpdatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await _store.UpsertAsync(new OrganizationResourceMapTemplate
        {
            Id = FacilityTemplateCatalog.CernerOrganizationResourceMapId,
            Name = "Cerner",
            Description = "Matches Cerner hospital locations: type system https://fhir.cerner.com/codeset/222 and RoleCode HOSP. Every location in the ehr-test3 Cerner bundle carries codeset 222; only the buildings also carry HOSP.",
            IsSystem = true,
            IsDefault = false,
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    FhirPath = FacilityTemplateCatalog.CernerOrgLocationFhirPath,
                    Priority = 1
                }
            ],
            UpdatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await _store.SetDefaultAsync(template.Id, cancellationToken);
        _logger.LogInformation("Seeded/refreshed system default organization resource map template: {Id}", template.Id);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
