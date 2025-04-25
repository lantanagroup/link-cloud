

using LantanaGroup.Link.DataAcquisition.Domain.Entities;

namespace LantanaGroup.Link.DataAcquisition.Application.Models;

public class FhirQueryModel
{
    public string FacilityId { get; set; }
    public FhirQueryTypeModel QueryType { get; set; }
    public List<ResourceTypeModel> ResourceTypes { get; set; }
    public List<string> QueryParameters { get; set; } = new List<string>();
    public List<ResourceReferenceTypeModel> ResourceReferenceTypes { get; set; }
    public int? Paged { get; set; }
    public string DataAcquisitionLogId { get; set; }

    public static FhirQueryModel FromDomain(FhirQuery fhirQuery)
    {
        return new FhirQueryModel
        {
            FacilityId = fhirQuery.FacilityId,
            QueryType = FhirQueryTypeModelUtilities.FromDomain(fhirQuery.QueryType),
            ResourceTypes = fhirQuery.ResourceTypes.Select(ResourceTypeModelUtilities.FromDomain).ToList(),
            QueryParameters = fhirQuery.QueryParameters,
            ResourceReferenceTypes = fhirQuery.ResourceReferenceTypes.Select(ResourceReferenceTypeModelUtilities.FromDomain).ToList(),
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
            ResourceTypes = model.ResourceTypes.Select(ResourceTypeModelUtilities.ToDomain).ToList(),
            QueryParameters = model.QueryParameters,
            ResourceReferenceTypes = model.ResourceReferenceTypes.Select(ResourceReferenceTypeModelUtilities.ToDomain).ToList(),
            Paged = model.Paged,
            DataAcquisitionLogId = model.DataAcquisitionLogId
        };
    }
}
