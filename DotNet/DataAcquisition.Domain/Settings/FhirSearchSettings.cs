namespace LantanaGroup.Link.DataAcquisition.Domain.Settings;

/// <summary>
/// FHIR <c>_count</c> (result page size). This is not filter-ID chunking
/// (<c>ResourceIdsParameter.Paged</c>).
/// </summary>
public class FhirSearchSettings
{
    public const string SectionName = "FhirSearch";
    public const int DefaultPageSize = 100;

    /// <summary>
    /// Result page size sent as FHIR <c>_count</c> when the query does not already set one.
    /// Values less than 1 fall back to <see cref="DefaultPageSize"/>.
    /// </summary>
    public int PageSize { get; set; } = DefaultPageSize;

    public int ResolvePageSize() => PageSize > 0 ? PageSize : DefaultPageSize;
}
