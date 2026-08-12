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
    /// Replaces the codes of an already-cached code group with the contents of a CSV, preserving the
    /// FHIR resource metadata (url, version, id, name, identifiers) loaded from disk.
    /// </summary>
    /// <remarks>
    /// Affects this instance's in-memory cache only: nothing is written to the configured terminology
    /// path, and <see cref="LoadCache"/> restores the on-disk state. The replacement is all-or-nothing —
    /// a CSV that fails to parse leaves the previously cached codes untouched.
    /// </remarks>
    /// <param name="type">The kind of code group to replace.</param>
    /// <param name="id">The resource id of the code group.</param>
    /// <param name="version">The version to replace, or null for the latest cached version.</param>
    /// <param name="csvContent">
    /// The CSV content, including a header row. A CodeSystem CSV has 2 or 3 columns (code, display and
    /// optionally status); a ValueSet CSV has 3 or 4 (system, code, display and optionally status).
    /// </param>
    /// <returns>The replaced code group, carrying the codes just loaded.</returns>
    /// <exception cref="KeyNotFoundException">No such code group is cached.</exception>
    /// <exception cref="InvalidOperationException">The CSV does not have a supported number of columns.</exception>
    CodeGroup ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes type, string id, string? version, string csvContent);

    /// <summary>
    /// Clears all cached code groups.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Loads (or reloads) the cache from the configured terminology source.
    /// </summary>
    Task LoadCache();
}
