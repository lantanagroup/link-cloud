using Automation.UI.Models;
using Automation.UI.Services.Persistence;

namespace Automation.UI.Services;

public sealed class OrganizationResourceMapTemplateResolver
{
    private readonly IOrganizationResourceMapTemplateStore _store;

    public OrganizationResourceMapTemplateResolver(IOrganizationResourceMapTemplateStore store)
    {
        _store = store;
    }

    public async Task<OrganizationResourceMapTemplate?> ResolveAsync(Guid? templateId, CancellationToken ct = default)
    {
        if (templateId.HasValue)
        {
            var selected = await _store.GetByIdAsync(templateId.Value, ct);
            if (selected == null)
                throw new InvalidOperationException($"Organization resource map template '{templateId.Value}' was not found.");
            return selected;
        }

        return await _store.GetDefaultAsync(ct);
    }
}
