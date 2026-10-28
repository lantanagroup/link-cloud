using Automation.UI.Models;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// Persistence contract for query plan templates.
/// </summary>
public interface IQueryPlanTemplateStore
{
    Task<List<QueryPlanTemplate>> GetAllAsync(CancellationToken ct = default);
    Task<QueryPlanTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<QueryPlanTemplate?> GetDefaultAsync(CancellationToken ct = default);
    Task UpsertAsync(QueryPlanTemplate template, CancellationToken ct = default);
    Task SetDefaultAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
