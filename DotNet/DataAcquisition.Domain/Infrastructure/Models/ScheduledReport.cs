using LantanaGroup.Link.Shared.Application.Models;

namespace DataAcquisition.Domain.Infrastructure.Models;
public class ScheduledReport
{
    public string[] ReportTypes { get; set; } = Array.Empty<string>();
    public Frequency Frequency { get; set; }
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
}
