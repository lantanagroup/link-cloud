using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Report.Application.Models
{
    public class AggregateResult
    {
        public Uri Uri;
        //public List<string> MeasureReportReferences = new List<string>();
        public List<AggregateMeasureReportResult> MeasureReportResults = new List<AggregateMeasureReportResult>();
    }

    public class AggregateMeasureReportResult {
        public string Measure;
        public List<AggregateMeasureReportPopulation> PopulationList = new List<AggregateMeasureReportPopulation>();
    }

    public class AggregateMeasureReportPopulation
    {
        public string PopulationId;
        public int PopulationCount;
        public CodeableConcept PopulationCode;
    }
}
