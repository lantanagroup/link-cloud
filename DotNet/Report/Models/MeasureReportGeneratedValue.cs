namespace LantanaGroup.Link.Report.Models
{
    public class MeasureReportGeneratedValue
    {
        public string FacilityId { get; set; } = string.Empty;
        public string ReportTrackingId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public string? MeasureReportId { get; set; }
        public string MeasureReportURI { get; set; } = string.Empty;
        public string MeasureReportBlobName { get; set; } = string.Empty;
        public bool Reportable { get; set; }
    }
}