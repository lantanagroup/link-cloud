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
        public List<ReportPopulation> ReportPopulations = new List<ReportPopulation>();
    }

    [BsonIgnoreExtraElements]
    public class ReportPopulation 
    {
        public string PopulationId { get; set; }
        //TODO: Temp string
        public string PopulationCode { get; set; }
        //public CodeableConcept PopulationCode { get; set; }
        public int TotalPopulationCount { get; set; }
        public List<MeasureReportPopulation> MeasureReportIds = new List<MeasureReportPopulation>();
    }

    public class MeasureReportPopulation 
    { 
        public string MeasureReportId { get; set; }
        public int PopulationCount { get; set; }
    }
}
