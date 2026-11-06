namespace LantanaGroup.Link.Report.Domain.Models;

/// <summary>
/// A report the retention check found eligible for cleanup.
/// </summary>
/// <param name="ReportScheduleId">
/// The report's id.
/// </param>
/// <param name="FacilityId">
/// The facility the report belongs to, carried on the cleanup request.
/// </param>
public sealed record RetentionEligibleReport(Guid ReportScheduleId, string FacilityId);
