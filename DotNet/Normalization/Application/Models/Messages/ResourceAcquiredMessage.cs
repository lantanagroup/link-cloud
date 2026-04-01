using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace LantanaGroup.Link.Normalization.Application.Models.Messages;

public class ResourceAcquiredMessage
{
    public bool AcquisitionComplete { get; set; } = false;
    public string PatientId { get; set; }
    public string QueryType { get; set; }
    public string? ResourceType { get; set; }
    public object Resource { get; set; }
    //public string Resource { get; set; }
    public string ReportableEvent { get; set; }
    public List<ScheduledReport> ScheduledReports { get; set; }
}
