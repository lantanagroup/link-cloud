using LantanaGroup.Link.Report.Attributes;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{

    [BsonCollection("patientsToQuery")]
    [BsonIgnoreExtraElements]
    public class PatientsToQueryModel : ReportEntity
    {
        public string FacilityId { get; set; } = string.Empty;
        public List<string>? PatientIds { get; set; }
    }
}
