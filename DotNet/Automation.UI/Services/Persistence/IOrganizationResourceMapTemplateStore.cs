using Automation.UI.Models;

namespace Automation.UI.Services.Persistence;

public interface IOrganizationResourceMapTemplateStore
{
    Task<List<OrganizationResourceMapTemplate>> GetAllAsync(CancellationToken ct = default);
    Task<OrganizationResourceMapTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<OrganizationResourceMapTemplate?> GetDefaultAsync(CancellationToken ct = default);
    Task UpsertAsync(OrganizationResourceMapTemplate template, CancellationToken ct = default);
    Task SetDefaultAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
