using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Converters;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Report.Entities
{
    public class PatientSubmissionModel : BaseEntityExtended
    {
        public string FacilityId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string ReportScheduleId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        [JsonConverter(typeof(FhirResourceConverter<Bundle>))]
        [BsonSerializer(typeof(MongoFhirBaseSerDes<Bundle>))]
        public Bundle PatientResources { get; set; }
        [JsonConverter(typeof(FhirResourceConverter<Bundle>))]
        [BsonSerializer(typeof(MongoFhirBaseSerDes<Bundle>))]
        public Bundle OtherResources { get; set; }
    }
}
