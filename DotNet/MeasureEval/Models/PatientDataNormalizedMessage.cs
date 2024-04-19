namespace LantanaGroup.Link.MeasureEval.Models;

public class PatientDataNormalizedMessage
{
    public string PatientId { get; set; }
    public object PatientBundle { get; set; }
    public List<ScheduledReport> ScheduledReports { get; set; }
}
