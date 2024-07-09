using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{

    [BsonCollection("patientsToQuery")]
    [BsonIgnoreExtraElements]
    public class PatientsToQueryModel : BaseEntity
    {
        public string FacilityId { get; set; } = string.Empty;
        public List<string>? PatientIds { get; set; }
    }
}
