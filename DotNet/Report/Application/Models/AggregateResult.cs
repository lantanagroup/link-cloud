using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Report.Application.Models
{
    public class AggregateResult
    {
        public Uri Uri;
        public List<AggregateMeasureReportResult> MeasureReportResults = new List<AggregateMeasureReportResult>();
    }

    public class AggregateMeasureReportResult {
        public string Measure;
        public string ReportType;
        public string MeasureReportId;
        public List<AggregateMeasureReportPopulation> PopulationList = new List<AggregateMeasureReportPopulation>();
        public Dictionary<string, int> ResourceCount = new Dictionary<string, int>();
        public List<string[]> Resources = new List<string[]>();
    }

    public class AggregateMeasureReportPopulation
    {
        public string PopulationId;
        public int PopulationCount;
        public CodeableConcept PopulationCode;
    }
}
