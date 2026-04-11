using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;

namespace DataAcquisition.Domain.Application.Models;

public class FhirQueryModel
{
    public Guid? Id { get; set; }
    public string? FacilityId { get; set; }
    public bool? IsReference { get; set; }
    public FhirQueryType QueryType { get; set; }
    public IEnumerable<Hl7.Fhir.Model.ResourceType> ResourceTypes { get; set; } = new List<Hl7.Fhir.Model.ResourceType>();
    public List<string> QueryParameters { get; set; } = new();
    public List<ResourceReferenceTypeModel> ResourceReferenceTypes { get; set; } = new();
    public int? Paged { get; set; }
    public long DataAcquisitionLogId { get; set; }
    public string? MeasureId { get; set; }
    public IEnumerable<string> IdQueryParameterValues { get; set; } = new List<string>();
    public TimeFrame? CensusTimeFrame { get; set; } = null;
    public ListType? CensusPatientStatus { get; set; } = null;
    public string? CensusListId { get; set; } = null;

    public string Query
    {
        get
        {
            if (ResourceTypes == null || !ResourceTypes.Any())
                return string.Empty;

            return QueryType switch
            {
                FhirQueryType.Search => $"{ResourceTypes.First()}?{string.Join("&", QueryParameters)}",
                FhirQueryType.SearchPost => $"{ResourceTypes.First()}/_search [{string.Join(",", QueryParameters)}]",
                FhirQueryType.Read => $"{ResourceTypes.First()}/{string.Join("&", QueryParameters)}",
                FhirQueryType.BulkDataRequest => "BulkDataRequest", // add logic when bulk fhir is implemented
                FhirQueryType.BulkDataPoll => string.Join("&", QueryParameters),
                _ => string.Empty
            };
        }
    }
}
