namespace LantanaGroup.Link.Report.Application.Models
{
    public class MeasureReportGeneratedValue
    {
        public string FacilityId { get; set; } = string.Empty;
        public string ReportTrackingId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public string MeasureReportId { get; set; }
        public string MeasureReportURI { get; set; } = string.Empty;
        //TODO: Blobname instead of file name
        public string MeasureReportFileName { get; set; } = string.Empty;
        public bool IsReportable { get; set; }
    }
}