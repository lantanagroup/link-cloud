namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Validation;

/// <summary>
/// The validation service's response for a report's stored category counts.
/// </summary>
public class ValidationReportCategoryCountsResponse
{
    /// <summary>
    /// The facility the report belongs to.
    /// </summary>
    public string FacilityId { get; set; } = string.Empty;

    /// <summary>
    /// The report the counts belong to.
    /// </summary>
    public string ReportId { get; set; } = string.Empty;

    /// <summary>
    /// When the report's counts last changed.
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; }

    /// <summary>
    /// The report's counts, most issues first. Severity and guidance are not set by the validation service.
    /// </summary>
    public List<ValidationCategoryCount> Categories { get; set; } = [];
}
