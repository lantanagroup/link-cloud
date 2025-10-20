using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace DataAcquisition.Domain.Application.Models;

public class FhirQueryModel
{
    public Guid? Id { get; set; }
    public string? FacilityId { get; set; }
    public bool? IsReference { get; set; }
    public FhirQueryType QueryType { get; set; }
    public List<Hl7.Fhir.Model.ResourceType> ResourceTypes { get; set; } = new();
    public List<string> QueryParameters { get; set; } = new();
    public List<ResourceReferenceTypeModel> ResourceReferenceTypes { get; set; } = new();
    public int? Paged { get; set; }
    public long DataAcquisitionLogId { get; set; }
    public string? MeasureId { get; set; }

    public string Query {
        get
        {
            if (ResourceTypes.Count == 0)
                return string.Empty;
            
            return QueryType switch
            {
                FhirQueryType.Search => $"{ResourceTypes[0]}?{string.Join("&", QueryParameters)}",
                FhirQueryType.Read => $"{ResourceTypes[0]}/{string.Join("&", QueryParameters)}",
                FhirQueryType.BulkDataRequest => string.Empty, // add logic when bulk fhir is implemented
                FhirQueryType.BulkDataPoll => string.Join("&", QueryParameters),
                _ => string.Empty
            };
        }
    }

    public List<string> IdQueryParameterValues { get; set; } = new();

    public static FhirQueryModel FromDomain(FhirQuery fhirQuery)
    {
        return new FhirQueryModel
        {
            Id = fhirQuery.Id,
            FacilityId = fhirQuery.FacilityId,
            MeasureId = fhirQuery.MeasureId,    
            IdQueryParameterValues = fhirQuery.IdQueryParameterValues.ToList(),
            IsReference = fhirQuery.IsReference,
            QueryType = fhirQuery.QueryType,
            ResourceTypes = fhirQuery.ResourceTypes,
            QueryParameters = fhirQuery.QueryParameters,
            ResourceReferenceTypes = fhirQuery.ResourceReferenceTypes.Select(ResourceReferenceTypeModel.FromDomain).ToList(),
            Paged = fhirQuery.Paged,
            DataAcquisitionLogId = fhirQuery.DataAcquisitionLogId
        };
    }
}
