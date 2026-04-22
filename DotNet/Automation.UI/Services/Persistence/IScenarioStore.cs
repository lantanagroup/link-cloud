using Automation.UI.Models;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// Abstraction for persisting and reading test scenario definitions.
/// </summary>
public interface IScenarioStore
{
    Task<List<TestScenarioDefinition>> GetAllAsync(CancellationToken ct = default);
    Task<TestScenarioDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpsertAsync(TestScenarioDefinition scenario, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
