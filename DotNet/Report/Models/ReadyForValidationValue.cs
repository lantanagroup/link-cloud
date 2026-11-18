namespace LantanaGroup.Link.Report.Models
{
    public class ReadyForValidationValue
    {
        public string? FacilityId { get; set; }
        public string PatientId { get; set; }
        public List<string> ReportTypes { get; set; }
        public string? ReportTrackingId { get; internal set; }
        public string? PayloadUri { get; set; }
    }
}
