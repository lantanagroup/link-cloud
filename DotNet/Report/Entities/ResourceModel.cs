using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{
    public class ResourceModel : BaseEntityExtended, IFacilityResource
    {
        public ResourceCategoryType ResourceCategoryType { get; set; }
        public string FacilityId { get; set; }
        public string ResourceType { get; set; }
        public string ResourceId { get; set; }

        [BsonSerializer(typeof(MongoFhirBaseSerDes<Resource>))]
        public Resource Resource { get; set; }

        public string GetId()
        {
            return Id;
        }

        public Resource GetResource()
        {
            return Resource;
        }
        public void SetResource(Resource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            Resource = resource;
        }
    }

    [BsonCollection("sharedResource")]
    public class SharedResourceModel : ResourceModel
    {
        
    }

    [BsonCollection("patientResource")]
    public class PatientResourceModel : ResourceModel
    {
        public string PatientId { get; set; }
    }
}
