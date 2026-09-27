using Automation.UI.Models;

namespace Automation.UI.Services.Persistence;

public interface IFacilityTemplateStore
{
    Task<List<FacilityTemplate>> GetAllAsync(CancellationToken ct = default);
    Task<FacilityTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<FacilityTemplate?> GetDefaultAsync(CancellationToken ct = default);
    Task UpsertAsync(FacilityTemplate template, CancellationToken ct = default);
    Task SetDefaultAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
