using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class FhirQueryModel
{
    public string FacilityId { get; set; }
    public FhirQueryType QueryType { get; set; }
    public List<Hl7.Fhir.Model.ResourceType> ResourceTypes { get; set; }
    public List<string> QueryParameters { get; set; } = [];
    public List<ResourceReferenceTypeModel> ResourceReferenceTypes { get; set; }
    public int? Paged { get; set; }
    public long DataAcquisitionLogId { get; set; }
    
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

    public static FhirQueryModel FromDomain(FhirQuery fhirQuery)
    {
        return new FhirQueryModel
        {
            FacilityId = fhirQuery.FacilityId,
            QueryType = fhirQuery.QueryType,
            ResourceTypes = fhirQuery.ResourceTypes,
            QueryParameters = fhirQuery.QueryParameters,
            ResourceReferenceTypes = fhirQuery.ResourceReferenceTypes.Select(ResourceReferenceTypeModel.FromDomain).ToList(),
            Paged = fhirQuery.Paged,
            DataAcquisitionLogId = fhirQuery.DataAcquisitionLogId
        };
    }

    public static FhirQuery ToDomain(FhirQueryModel model)
    {
        return new FhirQuery
        {
            FacilityId = model.FacilityId,
            QueryType = model.QueryType,
            ResourceTypes = model.ResourceTypes,
            QueryParameters = model.QueryParameters,
            ResourceReferenceTypes = model.ResourceReferenceTypes.Select(ResourceReferenceTypeModel.ToDomain).ToList(),
            Paged = model.Paged,
            DataAcquisitionLogId = model.DataAcquisitionLogId
        };
    }
}
