namespace LantanaGroup.Link.Terminology.Application.Models;

/// <summary>
/// Summarises the codes loaded into a code group by a CSV upload, so the caller can confirm what the
/// running instance now holds without a follow-up expand.
/// </summary>
public class ReplaceCodesResponse
{
    /// <summary>The kind of code group that was replaced, "CodeSystem" or "ValueSet".</summary>
    public required string Type { get; init; }

    /// <summary>The resource id of the replaced code group.</summary>
    public required string Id { get; init; }

    /// <summary>The version of the replaced code group, as loaded from its FHIR resource.</summary>
    public string? Version { get; init; }

    /// <summary>The total number of codes loaded from the CSV.</summary>
    public required int CodeCount { get; init; }

    /// <summary>
    /// The number of distinct code systems the codes belong to. Always 1 for a CodeSystem; a ValueSet
    /// spans as many systems as its CSV's first column names.
    /// </summary>
    public required int SystemCount { get; init; }

    /// <summary>
    /// How many of the loaded codes carry an Inactive status.
    /// </summary>
    /// <remarks>
    /// A ValueSet CSV without the optional fourth column loads members with no membership status at all,
    /// which reports zero here and leaves each code's status to be resolved from its code system. If a
    /// test expects inactive members, a non-zero count here is the confirmation that the status column
    /// was read.
    /// </remarks>
    public required int InactiveCodeCount { get; init; }

    /// <summary>The name of the uploaded file, echoed back to confirm which CSV was applied.</summary>
    public string? FileName { get; init; }
}
