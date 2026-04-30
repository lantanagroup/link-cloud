using System.ComponentModel.DataAnnotations;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;

public class LogSearchParameters : GenericLogSearchParameters
{
    public string? FacilityId { get; set; }
    public string? PatientId { get; set; }
    public string? ReportId { get; set; }
    public string? ResourceId { get; set; }
    public QueryPhase? QueryPhase { get; set; }
    public FhirQueryType? QueryType { get; set; }
    public List<RequestStatus>? Statuses { get; set; }
    public AcquisitionPriority? Priority { get; set; }
    public string? ResourceType { get; set; }
    public DateTime? CreatedBefore { get; set; }

    /// <summary>
    /// Free-text term applied as a case-insensitive substring match against the most
    /// commonly searched columns (PatientId, ResourceType names) plus an exact match
    /// against Id when the term parses as a numeric id. Combined with the structured
    /// filters via AND — within the term itself, the column predicates are OR’d.
    /// </summary>
    public string? SearchTerm { get; set; }
}

public class SftpLogSearchParameters : GenericLogSearchParameters
{
    public string? FacilityId { get; set; }
    public RequestStatus? Status { get; set; }
    public SftpAcquisitionType? AcquisitionType { get; set; }
    public SftpAcquisitionSubType? SubType { get; set; }
}

public class GenericLogSearchParameters
{
    [Range(1, int.MaxValue, ErrorMessage = "PageNumber must be greater than 0")]
    public int PageNumber { get; set; } = 1;
    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100")]
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "ExecutionDate";
    public SortOrder SortOrder { get; set; } = SortOrder.Descending;
    public bool IncludeDeleted { get; set; } = false;
}
