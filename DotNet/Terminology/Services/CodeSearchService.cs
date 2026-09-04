using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Terminology;
using LantanaGroup.Link.Terminology.Application.Extensions;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;

namespace LantanaGroup.Link.Terminology.Services;

/// <summary>
/// Searches the cached code groups for codes matching a query, returning a paged Link read model.
/// </summary>
/// <remarks>
/// <para>
/// The order of operations matters and is fixed: resolve scope, match, de-duplicate, filter by status,
/// order, count, then page. Counting before de-duplication would inflate <c>totalCount</c>, and paging
/// before ordering would let a record appear on two pages or on none.
/// </para>
/// <para>
/// Status is never computed here. <see cref="FhirService.ResolveCodeStatus"/> is the single implementation
/// and stays that way — LEGLINK-889 was raised because two code paths disagreed about one code, and the
/// value-set-membership precedence rule is the subtle part.
/// </para>
/// </remarks>
public class CodeSearchService(ICodeGroupCacheService cacheService, FhirService fhirService) : ICodeSearchService
{
    /// <summary>
    /// A matched code before status resolution, carrying the source kind so the cross-group
    /// de-duplication rule can be applied.
    /// </summary>
    private sealed record Candidate(string System, Code Code, bool FromCodeSystem);

    /// <inheritdoc />
    public Task<PagedConfigModel<TerminologyCodeModel>> Search(CodeSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize, 1, CodeSearchDefaults.MaxPageSize);
        var pageNumber = Math.Max(query.PageNumber, 1);

        var matches = CollectMatches(query, cancellationToken);

        // Status is resolved lazily. When excludeInactive is set it has to be known for every match in
        // order to filter, but otherwise only the page actually returned needs it — and resolving it for
        // a plain value set member rejoins the code system with a full scan, so resolving every match
        // would multiply the cost of a broad search by the size of the code system behind it.
        IEnumerable<Candidate> candidates = matches;

        if (query.ExcludeInactive)
        {
            candidates = candidates.Where(c => ResolveStatus(c) != CodeStatus.Inactive);
        }

        var ordered = Order(candidates, query.Search).ToList();

        var records = ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new TerminologyCodeModel
            {
                System = c.System,
                Code = c.Code.Value,
                Display = c.Code.Display,
                Status = ResolveStatus(c)
            })
            .ToList();

        return Task.FromResult(new PagedConfigModel<TerminologyCodeModel>(
            records,
            new PaginationMetadata(pageSize, pageNumber, ordered.Count)));
    }

    /// <summary>
    /// Walks every code group in scope and collects one candidate per distinct <c>(system, code)</c>.
    /// </summary>
    /// <remarks>
    /// Two de-duplication rules meet here. Within a single code group the last occurrence of a code wins,
    /// matching <c>FhirService.BuildMatchResult</c> and <c>ResolveCodeStatus</c> — a CSV may list a code
    /// twice with differing status and the later row is the effective one (LEGLINK-599/814). Across groups
    /// a code system beats a value set, because a cross-content search asks "what codes exist" and
    /// membership status is scoped to one value set, of which several may be in play. Between two value
    /// sets the last one walked wins; that is arbitrary but deterministic for a given cache load, and the
    /// precedence question is recorded on LEGLINK-1007.
    /// </remarks>
    private List<Candidate> CollectMatches(CodeSearchQuery query, CancellationToken cancellationToken)
    {
        var byKey = new Dictionary<(string System, string Code), Candidate>();

        foreach (var group in ResolveScope(query))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fromCodeSystem = group.Type == CodeGroup.CodeGroupTypes.CodeSystem;

            foreach (var (systemUri, codes) in group.Codes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var code in codes)
                {
                    if (!IsMatch(code, query.Search))
                    {
                        continue;
                    }

                    var key = (systemUri, code.Value);

                    // A value set never displaces a code system, so the defining source wins however
                    // the groups happen to be ordered.
                    if (byKey.TryGetValue(key, out var existing) && existing.FromCodeSystem && !fromCodeSystem)
                    {
                        continue;
                    }

                    byKey[key] = new Candidate(systemUri, code, fromCodeSystem);
                }
            }
        }

        return byKey.Values.ToList();
    }

    /// <summary>
    /// Determines which code groups the query searches.
    /// </summary>
    /// <remarks>
    /// A named code system or value set narrows the search to that one group; naming neither searches the
    /// latest version of every loaded code system and value set, which is what the mapping UI does before
    /// the user has chosen a target system. The controller has already rejected a request that names both,
    /// and one that constrains the search by nothing at all.
    /// </remarks>
    private List<CodeGroup> ResolveScope(CodeSearchQuery query)
    {
        if (query.CodeSystem is not null)
        {
            return [RequireGroup(CodeGroup.CodeGroupTypes.CodeSystem, query.CodeSystem, query.Version, CodeSearchParameters.CodeSystem)];
        }

        if (query.ValueSet is not null)
        {
            return [RequireGroup(CodeGroup.CodeGroupTypes.ValueSet, query.ValueSet, query.Version, CodeSearchParameters.ValueSet)];
        }

        var groups = cacheService.GetAllCodeGroups(CodeGroup.CodeGroupTypes.CodeSystem);
        groups.AddRange(cacheService.GetAllCodeGroups(CodeGroup.CodeGroupTypes.ValueSet));
        return groups;
    }

    /// <summary>
    /// Resolves a named code group, rejecting a URI or version that is not loaded.
    /// </summary>
    /// <remarks>
    /// <see cref="CodeGroupCacheServiceExtensions.GetCodeGroupExact"/> rather than <c>GetCodeGroup</c>:
    /// the latter falls back to the newest cached version when the requested one is absent, which would
    /// answer with a version the caller never asked for instead of telling them it is not loaded.
    /// </remarks>
    private CodeGroup RequireGroup(CodeGroup.CodeGroupTypes type, string identifier, string? version, string parameterName)
    {
        var (canonical, effectiveVersion) = SplitCanonical(identifier, version);

        var group = cacheService.GetCodeGroupExact(type, canonical, effectiveVersion);

        if (group is not null)
        {
            return group;
        }

        // Separate "no such URI" from "that URI is loaded, that version is not", so the 400 names the
        // parameter the caller can actually correct.
        if (!string.IsNullOrEmpty(effectiveVersion) && cacheService.GetCodeGroup(type, canonical) is not null)
        {
            throw new ArgumentException(
                $"No {type} version '{effectiveVersion}' is loaded for '{canonical}'.", CodeSearchParameters.Version);
        }

        throw new ArgumentException($"No {type} is loaded with the URI '{canonical}'.", parameterName);
    }

    /// <summary>
    /// Splits a canonical URI carrying a version suffix, e.g. "http://example.org/CodeSystem/x|1.0.0".
    /// </summary>
    /// <remarks>
    /// <c>GetCodeGroup</c> performs the same split internally and treats the suffix as the requested
    /// version, but a caller that compares the result against its own <c>version</c> argument would then
    /// never check a piped one. Splitting here means the version actually requested is the version
    /// verified. An explicit <c>version</c> parameter wins over a suffix.
    /// </remarks>
    private static (string Canonical, string? Version) SplitCanonical(string identifier, string? version)
    {
        var pipeIndex = identifier.IndexOf('|');

        if (pipeIndex < 0)
        {
            return (identifier, version);
        }

        var suffix = identifier[(pipeIndex + 1)..];

        return (identifier[..pipeIndex], string.IsNullOrEmpty(version) ? suffix : version);
    }

    /// <summary>
    /// Matches a code against the search text, case-insensitively, against both the code and its display.
    /// </summary>
    /// <remarks>
    /// A null search means the caller constrained the result set by code system or value set instead, and
    /// wants everything in that group.
    /// </remarks>
    private static bool IsMatch(Code code, string? search)
    {
        if (search is null)
        {
            return true;
        }

        return code.Value.Contains(search, StringComparison.OrdinalIgnoreCase)
               || code.Display.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Applies the total ordering: exact code matches first, then code, then system.
    /// </summary>
    /// <remarks>
    /// The order has to be total or page 2 is not stable against page 1 — two records comparing equal can
    /// swap between requests, and a record then appears twice or not at all. <c>(code, system)</c> is
    /// unique after de-duplication, so adding system as the final tie-breaker settles every tie. The
    /// comparisons are ordinal so the order does not shift with the server's culture.
    /// </remarks>
    private static IEnumerable<Candidate> Order(IEnumerable<Candidate> candidates, string? search)
    {
        return candidates
            .OrderByDescending(c => search is not null && string.Equals(c.Code.Value, search, StringComparison.OrdinalIgnoreCase))
            .ThenBy(c => c.Code.Value, StringComparer.Ordinal)
            .ThenBy(c => c.System, StringComparer.Ordinal);
    }

    /// <summary>
    /// Resolves a candidate's effective status through the one implementation that owns the rule.
    /// </summary>
    private CodeStatus ResolveStatus(Candidate candidate) =>
        fhirService.ResolveCodeStatus(candidate.Code, candidate.System);
}
