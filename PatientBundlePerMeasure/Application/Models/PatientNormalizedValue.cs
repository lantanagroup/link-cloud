namespace LantanaGroup.Link.PatientBundlePerMeasure.Application.Models
{
    public partial class PatientNormalizedValue
    {
        public string Bundle { get; set; }
        public List<ScheduledReport> ScheduledReports { get; set; }
    }
}
