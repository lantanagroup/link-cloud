using LantanaGroup.Link.Shared.Application.Enums;

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
    /// The report schedule's status in Report. Null when Report gives none.
    /// </summary>
    public ScheduleStatus? ReportStatus { get; set; }

    /// <summary>
    /// True while the report is still running, so the counts cover only the patients validated so far.
    /// False once the report has completed or been deleted.
    /// </summary>
    public bool InProgress { get; set; }

    /// <summary>
    /// The report's counts, most issues first.
    /// </summary>
    public List<ValidationCategoryCount> Categories { get; set; } = [];
}
