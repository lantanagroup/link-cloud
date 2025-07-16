using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class ScheduledReportModel
{
    public List<string> ReportTypes { get; set; }
    public Frequency Frequency { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ReportTrackingId { get; set; }

    public static ScheduledReportModel FromDomain(LantanaGroup.Link.Shared.Application.Models.ScheduledReport report)
    {
        return new ScheduledReportModel
        {
            ReportTypes = report.ReportTypes,
            Frequency = report.Frequency,
            StartDate = report.StartDate,
            EndDate = report.EndDate,
            ReportTrackingId = report.ReportTrackingId
        };
    }

    public static LantanaGroup.Link.Shared.Application.Models.ScheduledReport ToDomain(ScheduledReportModel model) 
    {
        return new LantanaGroup.Link.Shared.Application.Models.ScheduledReport
        {
            ReportTypes = model.ReportTypes,
            Frequency = model.Frequency,
            StartDate = model.StartDate,
            EndDate = model.EndDate
        };
    }
}
