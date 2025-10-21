using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Enums;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;

public class SearchDataAcquisitionLogRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "ExecutionDate";
    public SortOrder SortOrder { get; set; } = SortOrder.Ascending;
    public string? FacilityId { get; set; }
    public string? PatientId { get; set; }
    public string? ReportTrackingId { get; set; }
    public string? ResourceId { get; set; }
    public FhirQueryType? QueryType { get; set; }
    public QueryPhase? QueryPhase { get; set; }
    public AcquisitionPriority? AcquisitionPriority { get; set; }
    public List<RequestStatus>? RequestStatuses { get; set; }
    public string? CorrelationId { get; set; }
    public string? ResourceType { get; set; }
    public bool IncludeDeleted { get; set; } = false;
    public DateTime? CreatedBefore { get; set; }

    /// <summary>
    /// Free-text term applied as case-insensitive substring against PatientId and
    /// resource-type names, plus exact match on numeric Id when the term parses as long.
    /// Null/empty means no extra filtering.
    /// </summary>
    public string? SearchTerm { get; set; }
}