namespace LantanaGroup.Link.Report.Models
{
    public class MeasureReportPopulationModel
    {
        public int Id { get; set; }
        public int GroupPopulationId { get; set; }
        public string MeasureReportId { get; set; } = default;
        public int PopulationCount { get; set; }
    }
}
