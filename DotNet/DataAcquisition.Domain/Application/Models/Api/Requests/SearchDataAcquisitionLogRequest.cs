using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;

public class SearchDataAcquisitionLogRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "ExecutionDate";
    public SortOrder SortOrder { get; set; } = SortOrder.Ascending;
    public string? FacilityId { get; set; }
    public string? PatientId { get; set; }
    public string? ReportId { get; set; }
    public string? ResourceId { get; set; }
    public string? ResourceType { get; set; }
    public FhirQueryType? QueryType { get; set; }
    public QueryPhase? QueryPhase { get; set; }        
    public AcquisitionPriority? AcquisitionPriority { get; set; }
    public RequestStatus? RequestStatus { get; set; }
}