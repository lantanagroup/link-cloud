namespace LantanaGroup.Link.PatientBundlePerMeasure.Application.Models
{
    public partial class ScheduledReport
    {
        public string ReportType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
