using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.IdentityModel.Tokens;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public record QueryLogSummaryModel
{
    public string Id { get; init; } = null!;
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
        return new QueryLogSummaryModel
        {
            Id = log.Id,
            Priority = AcquisitionPriorityModelUtilities.FromDomain(log.Priority),
            FacilityId = log.FacilityId,
            PatientId = log.PatientId,
            ResourceTypes = log.FhirQuery.SelectMany(q => q.ResourceTypes)
                .Select(rt => rt.ToString())
                .Distinct()
                .ToList(),
            ResourceId = log.FhirQuery.IsNullOrEmpty() ? 
                string.Empty :
                log.FhirQuery.First().ResourceTypes[0] == Hl7.Fhir.Model.ResourceType.Patient ? 
                    log.PatientId : log.QueryType == FhirQueryType.Read ? 
                        log.FhirQuery.First().QueryParameters[0] : string.Empty,
            FhirVersion = log.FhirVersion ?? string.Empty,
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
