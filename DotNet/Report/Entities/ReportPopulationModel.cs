using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{
    [BsonCollection("reportPopulation")]
    [BsonIgnoreExtraElements]
    public class ReportPopulationModel : BaseEntityExtended
    {
        public string FacilityId { get; set; } = string.Empty;
        public string ReportScheduleId { get; set; } = string.Empty;
        public string Measure { get; set; }
        public string PopulationId { get; set; }
        public CodeableConcept PopulationCode { get; set; }
        public int PopulationCount { get; set; }
        public List<string> MeasureReportIds = new List<string>();
    }
}
