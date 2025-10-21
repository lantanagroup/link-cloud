using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Enums;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
public class LogSearchParameters : GenericLogSearchParameters
{
    public string? FacilityId { get; set; }
    public string? PatientId { get; set; }
    public string? ReportId { get; set; }
    public string? ResourceId { get; set; }
    public QueryPhase? QueryPhase { get; set; }
    public FhirQueryType? QueryType { get; set; }
    public RequestStatus? Status { get; set; }
    public AcquisitionPriority? Priority { get; set; }
}

public class GenericLogSearchParameters
{
    [Range(1, int.MaxValue, ErrorMessage = "PageNumber must be greater than 0")]
    public int PageNumber { get; set; } = 1;
    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100")]
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "ExecutionDate";
    public SortOrder SortOrder { get; set; } = SortOrder.Descending;
}
