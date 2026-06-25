using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

[DataContract]
public record QueryLogSummaryModel
{
    [DataMember]
    public long? Id { get; init; }

    [DataMember]
    public AcquisitionPriority Priority { get; init; }

    [DataMember]
    public string FacilityId { get; init; } = null!;

    [DataMember]
    public string? PatientId { get; init; } = null!;

    [DataMember]
    public List<string> ResourceTypes { get; init; } = null!;

    [DataMember]
    public string? ResourceId { get; init; } = null!;

    [DataMember]
    public string FhirVersion { get; init; } = null!;

    [DataMember]
    public FhirQueryType? QueryType { get; init; }

    [DataMember]
    public QueryPhase? QueryPhase { get; init; }

    [DataMember]
    public DateTime? ExecutionDate { get; init; }

    [DataMember]
    public DateTime CreateDate { get; init; }

    [DataMember]
    public DateTime? CompletionDate { get; init; }

    [DataMember]
    public int? RetryAttempts { get; init; }

    [DataMember]
    public RequestStatus? Status { get; init; }

    [DataMember]
    public bool IsDeleted { get; init; }

    [DataMember]
    public string? ReportTrackingId { get; init; }

    [DataMember]
    public bool IsReferenceLog { get; init; }

    public static QueryLogSummaryModel FromDomain(DataAcquisitionLogModel log)
    {
        if (log == null || string.IsNullOrEmpty(log.FacilityId))
        {
            throw new ArgumentException("Invalid DataAcquisitionLog for QueryLogSummaryModel conversion.");
        }

        var firstFhirQuery = log.FhirQuery?.FirstOrDefault();

        return new QueryLogSummaryModel
        {
            Id = log.Id,
            Priority = log.Priority,
            FacilityId = log.FacilityId,
            PatientId = log.PatientId,
            ResourceTypes = firstFhirQuery?.ResourceTypes.Select(rt => rt.ToString()).ToList() ?? new List<string>(),
            ResourceId = firstFhirQuery?.ResourceTypes.FirstOrDefault() == ResourceType.Patient
                ? log.PatientId
                : log.QueryType == FhirQueryType.Read
                    ? firstFhirQuery?.QueryParameters.FirstOrDefault()
                    : string.Empty,
            FhirVersion = log.FhirVersion ?? string.Empty,
            QueryType = log.QueryType,
            QueryPhase = log.QueryPhase,
            ExecutionDate = log.ExecutionDate,
            CreateDate = log.CreateDate,
            CompletionDate = log.CompletionDate,
            RetryAttempts = log.RetryAttempts,
            Status = log.Status,
            ReportTrackingId = log.ReportTrackingId,
            IsDeleted = log.IsDeleted,
            IsReferenceLog = log.FhirQuery?.Any(q => q.IsReference == true) == true
        };
    }
}

public class QueryLogSummaryModelResponse : IPagedModel<QueryLogSummaryModel>
{
    public List<QueryLogSummaryModel> Records { get; set; } = [];
    public PaginationMetadata Metadata { get; set; } = null!;
}