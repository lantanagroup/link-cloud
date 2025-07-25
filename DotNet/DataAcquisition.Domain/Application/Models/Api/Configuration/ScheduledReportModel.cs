using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

public class ScheduledReportModel
{
    public List<string> ReportTypes { get; set; }
    public Frequency Frequency { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ReportTrackingId { get; set; }

    public static ScheduledReportModel FromDomain(ScheduledReport report)
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

    public static ScheduledReport ToDomain(ScheduledReportModel model) 
    {
        return new ScheduledReport
        {
            ReportTypes = model.ReportTypes,
            Frequency = model.Frequency,
            StartDate = model.StartDate,
            EndDate = model.EndDate
        };
    }
}
