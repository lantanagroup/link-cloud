using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

public class FhirQueryModel
{
    public string FacilityId { get; set; }
    public FhirQueryTypeModel QueryType { get; set; }
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
                FhirQueryTypeModel.Search => $"{ResourceTypes[0]}?{string.Join("&", QueryParameters)}",
                FhirQueryTypeModel.Read => $"{ResourceTypes[0]}/{string.Join("&", QueryParameters)}",
                FhirQueryTypeModel.BulkDataRequest => string.Empty, // add logic when bulk fhir is implemented
                FhirQueryTypeModel.BulkDataPoll => string.Join("&", QueryParameters),
                _ => string.Empty
            };
        }
    }

    public static FhirQueryModel FromDomain(FhirQuery fhirQuery)
    {
        return new FhirQueryModel
        {
            FacilityId = fhirQuery.FacilityId,
            QueryType = FhirQueryTypeModelUtilities.FromDomain(fhirQuery.QueryType),
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
            QueryType = FhirQueryTypeModelUtilities.ToDomain(model.QueryType),
            ResourceTypes = model.ResourceTypes,
            QueryParameters = model.QueryParameters,
            ResourceReferenceTypes = model.ResourceReferenceTypes.Select(ResourceReferenceTypeModel.ToDomain).ToList(),
            Paged = model.Paged,
            DataAcquisitionLogId = model.DataAcquisitionLogId
        };
    }
}
