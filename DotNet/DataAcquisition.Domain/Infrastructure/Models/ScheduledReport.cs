using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
public class ScheduledReport
{
    public string[] ReportTypes { get; set; }
    public Frequency Frequency { get; set; }
    public string StartDate { get; set; }
    public string EndDate { get; set; }
}
