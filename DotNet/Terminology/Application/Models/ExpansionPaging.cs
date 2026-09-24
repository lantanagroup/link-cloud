namespace LantanaGroup.Link.Terminology.Application.Models;

/// <summary>
/// The names of the paging parameters the expansion endpoints accept, as FHIR spells them.
/// </summary>
/// <remarks>
/// Deliberately <c>count</c> and <c>offset</c> with no leading underscore. <c>_count</c> is the search
/// interaction's page-size parameter and means something different -- it bounds the number of Bundle
/// entries, not the number of codes inside one of them. The $expand operation defines its own
/// <c>count</c>/<c>offset</c> pair (https://build.fhir.org/valueset-operation-expand.html), and
/// <c>GET /ValueSet?url=</c> and <c>GET /CodeSystem?url=</c> reuse those names here so one code group's
/// contents are paged the same way however the caller reaches them. Renaming either to the underscored
/// form would silently stop paging working.
/// </remarks>
public static class ExpansionParameterNames
{
    /// <summary>The page-size parameter.</summary>
    public const string Count = "count";

    /// <summary>The zero-based index of the first code to return.</summary>
    public const string Offset = "offset";
}

/// <summary>
/// The fallback bounds applied to an expansion when configuration supplies none.
/// </summary>
/// <remarks>
/// These are the initialisers behind <see cref="Settings.TerminologyConfig.DefaultExpansionPageSize"/>
/// and <see cref="Settings.TerminologyConfig.MaxExpansionPageSize"/>, so a configuration store that
/// never got the rows still bounds every response rather than reverting to the unbounded behaviour
/// this exists to remove (LEGLINK-968). Sized from the measured cost of a Firely expansion: roughly
/// 200 bytes of POCO and 75 bytes of JSON per code, so a 5,000-code page is about 1 MB of POCOs and
/// 375 KB of response.
/// </remarks>
public static class ExpansionDefaults
{
    /// <summary>The page size used when the caller names no <c>count</c>.</summary>
    public const int DefaultPageSize = 1000;

    /// <summary>
    /// The largest page any single request can obtain. A larger <c>count</c> is reduced to this rather
    /// than rejected, and the reduced value is reported back in the response.
    /// </summary>
    public const int MaxPageSize = 5000;
}

/// <summary>
/// The bounds actually applied to one expansion, after defaults and clamping.
/// </summary>
/// <param name="Offset">The zero-based index of the first code to return. Never negative.</param>
/// <param name="Count">The number of codes to return. Never negative, never above the server maximum.</param>
public readonly record struct ExpansionPage(int Offset, int Count);

/// <summary>
/// Resolves a caller's <c>count</c>/<c>offset</c> into the page the server will actually return.
/// </summary>
public static class ExpansionPaging
{
    /// <summary>
    /// Applies the default page size, the server maximum and the validity rules to a caller's request.
    /// </summary>
    /// <remarks>
    /// A <c>count</c> above <paramref name="maxPageSize"/> is clamped rather than refused, matching
    /// <c>CodeSearchService</c>'s handling of an oversized <c>pageSize</c>. The caller is told what it
    /// actually got: <c>FhirService</c> echoes the resolved values into
    /// <c>ValueSet.expansion.parameter</c>, so a clamped response is self-describing rather than
    /// silently short.
    /// </remarks>
    /// <param name="count">The caller's <c>count</c>, or null if it named none.</param>
    /// <param name="offset">The caller's <c>offset</c>, or null if it named none.</param>
    /// <param name="defaultPageSize">The page size to use when <paramref name="count"/> is null.</param>
    /// <param name="maxPageSize">The server maximum, which <paramref name="count"/> cannot exceed.</param>
    /// <returns>The page to return, with both values already bounded.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="count"/> or <paramref name="offset"/> was supplied and negative. The message
    /// carries no <c>paramName</c>: <c>FhirController.BadRequestProblem</c> passes it straight into the
    /// Problem Details <c>detail</c>, where the " (Parameter 'count')" suffix
    /// <see cref="ArgumentException"/> appends would be framework noise shown to the caller.
    /// </exception>
    public static ExpansionPage Resolve(int? count, int? offset, int defaultPageSize, int maxPageSize)
    {
        if (count is < 0)
        {
            throw new ArgumentException($"The '{ExpansionParameterNames.Count}' parameter cannot be negative");
        }

        if (offset is < 0)
        {
            throw new ArgumentException($"The '{ExpansionParameterNames.Offset}' parameter cannot be negative");
        }

        // count=0 is not "unspecified": the spec defines it as asking how large the expansion is
        // without paying for the codes, so it must survive as a zero rather than fall back to the
        // default. Hence the null check rather than a falsy one.
        var resolvedCount = count ?? defaultPageSize;

        // The maximum is applied even to the configured default. Validation at startup already rejects
        // a default above the maximum, but clamping here means a hand-constructed FhirService in a test
        // cannot produce a page larger than the maximum it was given.
        return new ExpansionPage(offset ?? 0, Math.Clamp(resolvedCount, 0, Math.Max(maxPageSize, 0)));
    }
}
