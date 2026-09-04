namespace LantanaGroup.Link.Terminology.Application.Models;

/// <summary>
/// A validated, sanitized code search request.
/// </summary>
/// <remarks>
/// The controller owns sanitizing the caller's input and rejecting unusable parameter combinations; by the
/// time a query reaches <c>ICodeSearchService</c> its string values are clean and blank values have been
/// normalized to null, so the service can treat null as "not supplied" without re-checking for whitespace.
/// </remarks>
public class CodeSearchQuery
{
    /// <summary>
    /// Free text matched against both the code and its display, or null to return everything in scope.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>The canonical URI of the single code system to search, if the caller named one.</summary>
    public string? CodeSystem { get; init; }

    /// <summary>The canonical URI of the single value set to search, if the caller named one.</summary>
    public string? ValueSet { get; init; }

    /// <summary>
    /// The version of whichever of <see cref="CodeSystem"/> or <see cref="ValueSet"/> was supplied.
    /// Null selects the latest loaded version.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>When true, codes whose resolved status is inactive are omitted.</summary>
    public bool ExcludeInactive { get; init; }

    /// <summary>The 1-based page to return. Values below 1 are clamped.</summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// The maximum number of records to return. Clamped to the server maximum so that no request can
    /// return an entire code system.
    /// </summary>
    public int PageSize { get; init; } = CodeSearchDefaults.DefaultPageSize;
}

/// <summary>
/// The bounds the code search endpoint enforces, shared by the service that applies them and the
/// controller that documents them.
/// </summary>
public static class CodeSearchDefaults
{
    /// <summary>The page size used when the caller does not ask for one.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// The largest page the server will return. A search that matches more than this pages rather than
    /// returning the whole set, which is what keeps a bare three-character search off a 375,000-code
    /// code system.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// The shortest accepted <see cref="CodeSearchQuery.Search"/>. Matching is a linear scan of every code
    /// group in scope, so a one- or two-character search is refused rather than served slowly.
    /// </summary>
    public const int MinSearchLength = 3;
}
