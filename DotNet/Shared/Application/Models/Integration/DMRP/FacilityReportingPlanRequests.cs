namespace LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;

public class FacilityReportingPlanRequest
{
    public string? FacilityId { get; set; }

    public string? MeasureMappingId { get; set; }

    public int ReportingMonth { get; set; }

    public int ReportingYear { get; set; }

    public bool IsReporting { get; set; }
}

public class FacilityReportingPlanUpdateRequest : FacilityReportingPlanRequest
{
    public string? Id { get; set; }
}
