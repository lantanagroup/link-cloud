using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Attributes;
using MongoDB.Bson;
using System.Text.Json.Nodes;

namespace LantanaGroup.Link.Report.Entities
{
    [BsonCollection("patientResource")]
    public class PatientResourceModel : ReportEntity
    {
        public string FacilityId { get; set; }
        public string PatientId { get; set; }
        public string ResourceType { get; set; }
        public string ResourceId { get; set; }
        public string Resource { get; set; }
    }
}
