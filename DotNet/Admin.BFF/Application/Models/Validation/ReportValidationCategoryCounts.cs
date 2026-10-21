namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Validation;

/// <summary>
/// A report's stored validation counts per category, with each category's current guidance and severity.
/// </summary>
public class ReportValidationCategoryCounts
{
    /// <summary>
    /// The report schedule the counts belong to.
    /// </summary>
    public string ReportScheduleId { get; set; } = string.Empty;

    /// <summary>
    /// The facility the report belongs to.
    /// </summary>
    public string FacilityId { get; set; } = string.Empty;

    /// <summary>
    /// When the report's counts last changed.
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; }

    /// <summary>
    /// The report's counts, most issues first.
    /// </summary>
    public List<ValidationCategoryCount> Categories { get; set; } = [];
}
