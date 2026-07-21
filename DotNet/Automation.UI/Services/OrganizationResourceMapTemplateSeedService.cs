using Automation.UI.Models;
using Automation.UI.Services.Persistence;

namespace Automation.UI.Services;

public sealed class OrganizationResourceMapTemplateSeedService : IHostedService
{
    private static readonly Guid SystemDefaultTemplateId = new("00000000-0000-0000-3000-000000000100");

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
        await _store.SetDefaultAsync(template.Id, cancellationToken);
        _logger.LogInformation("Seeded/refreshed system default organization resource map template: {Id}", template.Id);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
