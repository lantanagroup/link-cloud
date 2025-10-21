using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public record QueryLogSummaryModel
{
    public long? Id { get; init; }
    public AcquisitionPriorityModel Priority { get; init; }
    public string FacilityId { get; init; } = null!;
    public string? PatientId { get; init; } = null!;
    public List<string> ResourceTypes { get; init; } = null!;
    public string? ResourceId { get; init; } = null!;
    public string FhirVersion { get; init; } = null!;
    public FhirQueryTypeModel QueryType { get; init; }
    public QueryPhaseModel QueryPhase { get; init; }
    public DateTime? ExecutionDate { get; init; }
    public RequestStatusModel Status { get; init; }

    public static QueryLogSummaryModel FromDomain(DataAcquisitionLog log)
    {
        if (log == null || !DataAcquisitionLog.ValidateForQuerySummaryLog(log))
        {
            throw new ArgumentException("Invalid DataAcquisitionLog for QueryLogSummaryModel conversion.");
        }

        var firstFhirQuery = log.FhirQuery?.FirstOrDefault();

        return new QueryLogSummaryModel
        {
            Id = log.Id,
            Priority = AcquisitionPriorityModelUtilities.FromDomain(log.Priority),
            FacilityId = log.FacilityId,
            PatientId = log.PatientId,
            ResourceTypes = firstFhirQuery?.ResourceTypes.Select(rt => rt.ToString()).ToList() ?? new List<string>(), // Fix for CS0029
            ResourceId = firstFhirQuery?.ResourceTypes.FirstOrDefault() == Hl7.Fhir.Model.ResourceType.Patient
                ? log.PatientId
                : log.QueryType == FhirQueryType.Read
                    ? firstFhirQuery?.QueryParameters.FirstOrDefault()
                    : string.Empty,
            FhirVersion = log.FhirVersion ?? string.Empty, // Fix for CS8601: Provide a default value if FhirVersion is null
            QueryType = FhirQueryTypeModelUtilities.FromDomain(log.QueryType.GetValueOrDefault()),
            QueryPhase = QueryPhaseModelUtilities.FromDomain(log.QueryPhase.GetValueOrDefault()),
            ExecutionDate = log.ExecutionDate,
            Status = RequestStatusModelUtilities.FromDomain(log.Status.GetValueOrDefault())
        };
    }
}

public class QueryLogSummaryModelResponse : IPagedModel<QueryLogSummaryModel>
{
    public List<QueryLogSummaryModel> Records { get; set; } = [];
    public PaginationMetadata Metadata { get; set; } = null!;
}
