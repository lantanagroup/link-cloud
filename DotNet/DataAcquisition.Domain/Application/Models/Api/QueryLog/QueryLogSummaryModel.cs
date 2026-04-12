using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using ResourceType = Hl7.Fhir.Model.ResourceType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

public record QueryLogSummaryModel
{
    public long? Id { get; init; }
    public AcquisitionPriority Priority { get; init; }
    public string FacilityId { get; init; } = null!;
    public string? PatientId { get; init; } = null!;
    public List<string> ResourceTypes { get; init; } = null!;
    public string? ResourceId { get; init; } = null!;
    public string FhirVersion { get; init; } = null!;
    public FhirQueryType? QueryType { get; init; }
    public QueryPhase? QueryPhase { get; init; }
    public DateTime? ExecutionDate { get; init; }
    public DateTime CreateDate { get; init; }
    public DateTime? CompletionDate { get; init; }
    public int? RetryAttempts { get; init; }
    public RequestStatus? Status { get; init; }
    public bool IsDeleted { get; init; }
    public string? ReportTrackingId { get; init; }
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
            IsReferenceLog = log.FhirQuery?.Any(q => q.IsReference == true) == true
        };
    }
}

public class QueryLogSummaryModelResponse : IPagedModel<QueryLogSummaryModel>
{
    public List<QueryLogSummaryModel> Records { get; set; } = [];
    public PaginationMetadata Metadata { get; set; } = null!;
}