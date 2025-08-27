using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Enums;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
public class LogSearchParameters : GenericLogSearchParameters
{
    [Required]
    public required string? FacilityId { get; set; }
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
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "ExecutionDate";
    public SortOrder SortOrder { get; set; } = SortOrder.Descending;
}
