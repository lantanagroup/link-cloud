using LantanaGroup.Automation.Generation;

namespace Automation.UI.Services.Persistence;

public interface IGenerationCatalogStore
{
    Task<List<GenerationCatalogItem>> GetAllAsync(CancellationToken ct = default);
    Task MergeAsync(IEnumerable<GenerationCatalogItem> items, CancellationToken ct = default);
}
