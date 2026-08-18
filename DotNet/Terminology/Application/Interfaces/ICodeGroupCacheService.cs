using LantanaGroup.Link.Terminology.Application.Models;

namespace LantanaGroup.Link.Terminology.Application.Interfaces;

/// <summary>
/// Public contract for the code group cache: retrieval of CodeSystem/ValueSet code groups
/// and cache management. Consumers depend on this abstraction so the cache can be substituted in tests.
/// </summary>
public interface ICodeGroupCacheService
{
    /// <summary>
    /// Retrieves a code group by its id, optionally constrained to a specific version.
    /// </summary>
    CodeGroup? GetCodeGroupById(CodeGroup.CodeGroupTypes type, string id, string? version = null);

    /// <summary>
    /// Retrieves a code group by its canonical URL (or identifier), optionally constrained to a specific version.
    /// </summary>
    CodeGroup? GetCodeGroup(CodeGroup.CodeGroupTypes type, string identifier, string? version = null);

    /// <summary>
    /// Retrieves the latest version of every code group of the given type.
    /// </summary>
    List<CodeGroup> GetAllCodeGroups(CodeGroup.CodeGroupTypes type);

    /// <summary>
    /// Clears all cached code groups.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Loads (or reloads) the cache from the configured terminology source.
    /// </summary>
    Task LoadCache();
}
