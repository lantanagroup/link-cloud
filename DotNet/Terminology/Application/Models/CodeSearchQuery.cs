using System.Net;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LantanaGroup.Link.Terminology.Application.Models;

/// <summary>
/// A code search request, sanitized as it is bound and checked by <see cref="Validate"/>.
/// </summary>
/// <remarks>
/// Sanitizing happens in the property setters rather than in the controller so that it applies however the
/// query is constructed, and so that <see cref="Validate"/> measures the search length against the cleaned
/// value. A value that sanitizes away to nothing is left as an empty string and rejected by
/// <see cref="Validate"/> rather than silently treated as omitted: dropping a blank <c>codeSystem</c> would
/// widen the search to every loaded code group, which is not what the caller asked for.
/// </remarks>
public class CodeSearchQuery
{
    private readonly string? _search;
    private readonly string? _codeSystem;
    private readonly string? _valueSet;
    private readonly string? _version;

    /// <summary>
    /// Free text matched against both the code and its display, or null to return everything in scope.
    /// </summary>
    [FromQuery(Name = CodeSearchParameters.Search)]
    public string? Search
    {
        get => _search;
        init => _search = Clean(value);
    }

    /// <summary>The canonical URI of the single code system to search, if the caller named one.</summary>
    [FromQuery(Name = CodeSearchParameters.CodeSystem)]
    public string? CodeSystem
    {
        get => _codeSystem;
        init => _codeSystem = Clean(value);
    }

    /// <summary>The canonical URI of the single value set to search, if the caller named one.</summary>
    [FromQuery(Name = CodeSearchParameters.ValueSet)]
    public string? ValueSet
    {
        get => _valueSet;
        init => _valueSet = Clean(value);
    }

    /// <summary>
    /// The version of whichever of <c>codeSystem</c> or <c>valueSet</c> was supplied.
    /// Null selects the latest loaded version.
    /// </summary>
    [FromQuery(Name = CodeSearchParameters.Version)]
    public string? Version
    {
        get => _version;
        init => _version = Clean(value);
    }

    /// <summary>When true, codes whose resolved status is inactive are omitted.</summary>
    [FromQuery(Name = "excludeInactive")]
    public bool ExcludeInactive { get; init; }

    /// <summary>The 1-based page to return. Values below 1 are clamped.</summary>
    [FromQuery(Name = "pageNumber")]
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// The maximum number of records to return. Clamped to the server maximum so that no request can
    /// return an entire code system.
    /// </summary>
    [FromQuery(Name = "pageSize")]
    public int PageSize { get; init; } = CodeSearchDefaults.DefaultPageSize;

    /// <summary>
    /// Strips markup from a bound value while leaving the text itself intact.
    /// </summary>
    /// <remarks>
    /// <see cref="HtmlInputSanitizer.Sanitize"/> removes markup but also HTML-encodes what survives, so an
    /// "&amp;" arrives as "&amp;amp;" and stops matching cached content — over 2,000 LOINC displays contain
    /// one. Decoding afterwards restores the plain text while still dropping the markup.
    /// <c>SanitizeAndRemove</c> is not usable here: it strips every character outside
    /// <c>[A-Za-z0-9-_. ]</c>, which deletes the ":" and "/" from every canonical URI.
    /// <c>FhirController.SanitizeTerminologyValue</c> and <c>ConfigController.SanitizeLookupValue</c> do the
    /// same thing for the same reason; the three must agree or a code found by <c>$validate-code</c> is
    /// missing here. Null is preserved rather than becoming empty, so "not supplied" stays distinguishable
    /// from "supplied blank".
    /// </remarks>
    private static string? Clean(string? value) =>
        value is null ? null : WebUtility.HtmlDecode(value.Sanitize());

    /// <summary>
    /// Checks the parameter combination and returns the errors, keyed by query parameter name.
    /// </summary>
    /// <remarks>
    /// Every error is collected rather than short-circuiting on the first, so a caller who got two things
    /// wrong is told about both.
    /// </remarks>
    /// <returns>
    /// A dictionary that is valid when the query is usable, and otherwise carries one entry per offending
    /// parameter.
    /// </returns>
    public ModelStateDictionary Validate()
    {
        var modelState = new ModelStateDictionary();

        // Blank is rejected rather than treated as omitted. The check is on the sanitized value:
        // markup-only input such as "<b></b>" is not whitespace but sanitizes to nothing.
        if (IsBlank(Search))
        {
            modelState.AddModelError(CodeSearchParameters.Search, "Search cannot be blank.");
        }

        if (IsBlank(CodeSystem))
        {
            modelState.AddModelError(CodeSearchParameters.CodeSystem, "Code system cannot be blank.");
        }

        if (IsBlank(ValueSet))
        {
            modelState.AddModelError(CodeSearchParameters.ValueSet, "Value set cannot be blank.");
        }

        if (IsBlank(Version))
        {
            modelState.AddModelError(CodeSearchParameters.Version, "Version cannot be blank.");
        }

        if (CodeSystem is not null && ValueSet is not null)
        {
            const string error = "Cannot specify both a code system and a value set.";
            modelState.AddModelError(CodeSearchParameters.CodeSystem, error);
            modelState.AddModelError(CodeSearchParameters.ValueSet, error);
        }

        if (!string.IsNullOrEmpty(Version) && CodeSystem is null && ValueSet is null)
        {
            modelState.AddModelError(
                CodeSearchParameters.Version, "Version can only be supplied with a code system or value set.");
        }

        if (Search is not null && Search.Length < CodeSearchDefaults.MinSearchLength)
        {
            modelState.AddModelError(
                CodeSearchParameters.Search,
                $"Search must be at least {CodeSearchDefaults.MinSearchLength} characters.");
        }

        // A request naming none of the three would page the entire loaded terminology set, so it is
        // refused rather than answered with page 1 of everything.
        if (Search is null && CodeSystem is null && ValueSet is null)
        {
            modelState.AddModelError(
                CodeSearchParameters.Search, "Search, code system or value set must be supplied.");
        }

        return modelState;
    }

    /// <summary>Determines whether a supplied value is present but carries no usable text.</summary>
    private static bool IsBlank(string? value) => value is not null && value.Trim().Length == 0;
}

/// <summary>
/// The query parameter names the code search endpoint reports in its validation errors.
/// </summary>
/// <remarks>
/// Held as constants so the model and <c>CodeSearchService</c> name a parameter the way the caller spelled
/// it in the query string. <c>nameof</c> is not usable: the properties are PascalCase, the parameters are
/// not.
/// </remarks>
public static class CodeSearchParameters
{
    /// <summary>The free-text search parameter.</summary>
    public const string Search = "search";

    /// <summary>The code system canonical URI parameter.</summary>
    public const string CodeSystem = "codeSystem";

    /// <summary>The value set canonical URI parameter.</summary>
    public const string ValueSet = "valueSet";

    /// <summary>The code group version parameter.</summary>
    public const string Version = "version";
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
