using Automation.UI.Models;

namespace Automation.UI.Services.Persistence;

public interface IMeasureTemplateStore
{
    Task<List<MeasureTemplate>> GetAllAsync(CancellationToken ct = default);
    Task<MeasureTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<MeasureTemplate>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task UpsertAsync(MeasureTemplate template, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
