namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Facility;

public class FacilitySoftDeleteResult
{
    public string FacilityId { get; set; } = string.Empty;
    public bool TenantDeleted { get; set; }
    public bool ReportSchedulesDeleted { get; set; }
    public bool AcquisitionLogsDeleted { get; set; }
    public bool CensusJobsDeleted { get; set; }
}
