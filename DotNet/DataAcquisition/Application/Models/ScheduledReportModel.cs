using LantanaGroup.Link.Shared.Application.Models;
using ScheduledReport = DataAcquisition.Domain.Models.ScheduledReport;

namespace LantanaGroup.Link.DataAcquisition.Application.Models;

public class ScheduledReportModel
{
    public string[] ReportTypes { get; set; }
    public Frequency Frequency { get; set; }
    public string StartDate { get; set; }
    public string EndDate { get; set; }

    public static ScheduledReportModel FromDomain(ScheduledReport report)
    {
        return new ScheduledReportModel
        {
            ReportTypes = report.ReportTypes,
            Frequency = report.Frequency,
            StartDate = report.StartDate,
            EndDate = report.EndDate
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
