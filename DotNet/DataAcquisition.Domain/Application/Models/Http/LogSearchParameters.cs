using System.ComponentModel.DataAnnotations;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;

[DataContract]
public class LogSearchParameters : GenericLogSearchParameters
{
    [DataMember]
    public string? FacilityId { get; set; }

    [DataMember]
    public string? PatientId { get; set; }

    [DataMember]
    public string? ReportId { get; set; }

    [DataMember]
    public string? ResourceId { get; set; }

    [DataMember]
    public QueryPhase? QueryPhase { get; set; }

    [DataMember]
    public FhirQueryType? QueryType { get; set; }

    [DataMember]
    public List<RequestStatus>? Statuses { get; set; }

    [DataMember]
    public AcquisitionPriority? Priority { get; set; }

    [DataMember]
    public string? ResourceType { get; set; }

    [DataMember]
    public DateTime? CreatedBefore { get; set; }

    /// <summary>
    /// Free-text term applied as a case-insensitive substring match against the most
    /// commonly searched columns (PatientId, ResourceType names) plus an exact match
    /// against Id when the term parses as a numeric id. Combined with the structured
    /// filters via AND — within the term itself, the column predicates are OR’d.
    /// </summary>
    [DataMember]
    public string? SearchTerm { get; set; }
}

[DataContract]
public class SftpLogSearchParameters : GenericLogSearchParameters
{
    [DataMember]
    public string? FacilityId { get; set; }

    [DataMember]
    public RequestStatus? Status { get; set; }

    [DataMember]
    public SftpAcquisitionType? AcquisitionType { get; set; }

    [DataMember]
    public SftpAcquisitionSubType? SubType { get; set; }
}

[DataContract]
public class GenericLogSearchParameters
{
    [DataMember]
    [Range(1, int.MaxValue, ErrorMessage = "PageNumber must be greater than 0")]
    public int PageNumber { get; set; } = 1;

    [DataMember]
    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100")]
    public int PageSize { get; set; } = 10;

    [DataMember]
    public string SortBy { get; set; } = "ExecutionDate";

    [DataMember]
    public SortOrder SortOrder { get; set; } = SortOrder.Descending;

    [DataMember]
    public bool IncludeDeleted { get; set; } = false;
}
