namespace LantanaGroup.Link.Report.Application.Models
{
    public class MeasureEvaluatedKey
    {

        public string FacilityId { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

    }
}
