namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

public class DataAcquisitionLogStatusStatistics
{
    public string ReportId { get; set; } = string.Empty;
    public string? PatientId { get; set; }
    public List<DataAcquisitionLogStatusCount> Statuses { get; set; } = new();
}

public class DataAcquisitionLogStatusCount
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}
