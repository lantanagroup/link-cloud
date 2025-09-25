using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Report.Application.Models;

public class GetMeasureReportsQueryParameters
{
    public string? ReportId { get; set; }
    public string? PatientId { get; set; }
    public string? MeasureReportId { get; set; }
    public string? Measure { get; set; }
    public PatientSubmissionStatus? ReportStatus { get; set; }
    public ValidationStatus? ValidationStatus { get; set; }
    public string? SortBy { get; set; }
    public SortOrder SortOrder { get; set; } = SortOrder.Descending;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
