namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Facility;

public class FacilityRestoreResult
{
    public string FacilityId { get; set; } = string.Empty;
    public bool TenantRestored { get; set; }
    public bool ReportSchedulesRestored { get; set; }
    public bool AcquisitionLogsRestored { get; set; }
    public bool CensusJobsRestored { get; set; }
}
