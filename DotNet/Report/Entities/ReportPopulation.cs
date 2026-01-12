using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{
    [BsonCollection("reportPopulation")]
    [BsonIgnoreExtraElements]
    public class ReportPopulation : BaseEntityExtended
    {
        public string FacilityId { get; set; } = string.Empty;
        public string ReportScheduleId { get; set; } = string.Empty;
        public string Measure { get; set; }
        public string ReportType { get; set; }
        public List<GroupPopulation> GroupPopulationList = new List<GroupPopulation>();
    }

    [BsonIgnoreExtraElements]
    public class GroupPopulation
    {
        public string PopulationId { get; set; }
        [BsonSerializer(typeof(MongoFhirBaseSerDes<CodeableConcept>))]
        public CodeableConcept PopulationCode { get; set; }
        public int TotalPopulationCount { get; set; }
        public List<GroupPopulationMeasureReport> GroupPopulationMeasureReportList = new List<GroupPopulationMeasureReport>();
    }

    public class GroupPopulationMeasureReport
    {
        public string MeasureReportId { get; set; }
        public int PopulationCount { get; set; }
    }
}