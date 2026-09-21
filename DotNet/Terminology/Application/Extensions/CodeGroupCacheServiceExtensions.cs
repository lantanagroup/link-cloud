using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;

namespace LantanaGroup.Link.Terminology.Application.Extensions;

/// <summary>
/// Lookups over <see cref="ICodeGroupCacheService"/> that the interface itself does not offer.
/// </summary>
/// <remarks>
/// Extensions rather than interface members so that existing callers and their test doubles are
/// unaffected: each one composes the methods the interface already exposes.
/// </remarks>
public static class CodeGroupCacheServiceExtensions
{
    /// <summary>
    /// Retrieves a code group only when the requested version is the one cached.
    /// </summary>
    /// <remarks>
    /// <see cref="ICodeGroupCacheService.GetCodeGroup"/> falls back to the latest
    /// cached version when the requested one is absent (CodeGroupCacheService.cs:137),
    /// so a caller that must honour an explicit version cannot use it directly — it
    /// would answer with a version the caller never asked for. The comparison matches
    /// the one GetCodeGroup selects candidates with at :135; using a different one
    /// here could reject a version the cache just accepted.
    /// </remarks>
    public static CodeGroup? GetCodeGroupExact(
        this ICodeGroupCacheService cache,
        CodeGroup.CodeGroupTypes type,
        string identifier,
        string? version = null)
    {
        var group = cache.GetCodeGroup(type, identifier, version);

        if (group is null)
        {
            return null;
        }

        // IsNullOrEmpty rather than a null check: a blank version means "not supplied" throughout this
        // service (see ConfigController), and the guard this replaced skipped the comparison for one.
        return !string.IsNullOrEmpty(version) &&
               !string.Equals(group.Version, version, StringComparison.CurrentCultureIgnoreCase)
            ? null
            : group;
    }
}