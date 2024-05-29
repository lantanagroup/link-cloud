using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Attributes;
using LantanaGroup.Link.Report.Serializers;
using MongoDB.Bson.Serialization.Attributes;
using LantanaGroup.Link.Shared.Application.SerDes;

namespace LantanaGroup.Link.Report.Entities
{
    [BsonCollection("patientResource")]
    public class PatientResourceModel : ReportEntity, IFacilityResource
    {
        public string FacilityId { get; set; }
        public string PatientId { get; set; }
        public string ResourceType { get; set; }
        public string ResourceId { get; set; }
        [BsonSerializer(typeof(MongoFhirBaseSerDes<Resource>))]
        public Resource Resource { get; set; }

        public string GetId()
        {
            return this.Id;
        }

        Resource IFacilityResource.Resource()
        {
            return Resource;
        }
    }
}
