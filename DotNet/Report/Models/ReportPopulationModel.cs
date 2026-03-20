
namespace LantanaGroup.Link.Report.Models;
public class ReportPopulationModel
{
    public Guid Id { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public Guid ReportScheduleId { get; set; }
    public string? Measure { get; set; }
    public string ReportType { get; set; } = string.Empty;

    public List<GroupPopulationModel> GroupPopulations { get; set; } = new();
}

public class GroupPopulationModel
{
    public string PopulationId { get; set; } = string.Empty;
    public string? PopulationCodeJson { get; set; }
    public int TotalPopulationCount { get; set; }
    public IEnumerable<MeasureReportPopulationModel> MeasureReportPopulations { get; internal set; }
}