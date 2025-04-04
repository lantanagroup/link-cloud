namespace LantanaGroup.Link.Report.Application.Models
{
    public class ReadyForValidationValue
    {
        public string PatientId { get; set; }
        public List<string> ReportTypes { get; set; }
        public string? ReportTrackingId { get; internal set; }
    }
}
