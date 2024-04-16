using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Attributes;
using LantanaGroup.Link.Report.Serializers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Entities
{

    [BsonCollection("measureReportSubmissionEntry")]
    [BsonIgnoreExtraElements]
    public class MeasureReportSubmissionEntryModel : ReportEntity
    {
        public string FacilityId { get; set; } = string.Empty;
        public string MeasureReportScheduleId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string MeasureReport { get; private set; }
        public bool ReadyForSubmission { get; private set; } = false;
        public List<ContainedResource> ContainedResources { get; private set; } = new List<ContainedResource>();

        public class ContainedResource
        {
            public string Reference { get; set; } = string.Empty;
            public string Resource { get; set; }
        }

        public void AddMeasureReport(MeasureReport measureReport)
        {
            var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

            MeasureReport = JsonSerializer.Serialize(measureReport, options).ToJson();

            foreach (var evaluatedResource in measureReport.EvaluatedResource)
            {
                //If the resource is already in the list, skip it
                if (ContainedResources.Any(x => x.Reference == evaluatedResource.Reference))
                { 
                    continue;
                }

                ContainedResources.Add(new ContainedResource
                {
                    Reference = evaluatedResource.Reference
                });
            }

            ReadyForSubmission = ContainedResources.All(x => !string.IsNullOrWhiteSpace(x.Resource) && !string.IsNullOrWhiteSpace(MeasureReport));
        }

        public void AddContainedResource(Resource resource) 
        {
            var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

            var containedResource = ContainedResources.Where(x => x.Reference == resource.TypeName + "/" + resource.Id).FirstOrDefault();

            if (containedResource == null)
            {
                ContainedResources.Add(new ContainedResource
                {
                    Reference = resource.TypeName + "/" + resource.Id,
                    Resource = JsonSerializer.Serialize(resource, options).ToJson()
                });
            }
            else
            {
                containedResource.Resource = JsonSerializer.Serialize(resource, options).ToJson();
            }

            ReadyForSubmission = ContainedResources.All(x => !string.IsNullOrWhiteSpace(x.Resource) && !string.IsNullOrWhiteSpace(MeasureReport));
        }
    }
}
