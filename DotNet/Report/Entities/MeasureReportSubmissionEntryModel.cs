using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Attributes;
using LantanaGroup.Link.Report.Domain.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{

    [BsonCollection("measureReportSubmissionEntry")]
    [BsonIgnoreExtraElements]
    public class MeasureReportSubmissionEntryModel : ReportEntity
    {
        public string FacilityId { get; set; } = string.Empty;
        public string MeasureReportScheduleId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string MeasureReport { get; set; }
        public bool ReadyForSubmission { get; private set; } = false;
        public List<ContainedResource> ContainedResources { get; private set; } = new List<ContainedResource>();

        public class ContainedResource
        {
            //TODO: Daniel - Maybe enum instead of bool
            public bool IsPatientResource { get; set; }
            public string Reference { get; set; } = string.Empty;
            public string DocumentId { get; set; }
        }

        public  void AddMeasureReport(MeasureReport measureReport)
        {
            MeasureReport =  new FhirJsonSerializer().SerializeToString(measureReport);

            foreach (var evaluatedResource in measureReport.EvaluatedResource)
            {
                //If the resource is already in the list, skip it
                if (ContainedResources.Any(x => x.Reference == evaluatedResource.Reference))
                { 
                    continue;
                }

                bool isPatientResourceType = PatientResourceProvider.GetPatientResourceTypes().Any(x => x == evaluatedResource.TypeName);

                ContainedResources.Add(new ContainedResource
                {
                    Reference = evaluatedResource.Reference,
                    IsPatientResource = isPatientResourceType
                });
            }

            ReadyForSubmission = ContainedResources.All(x => !string.IsNullOrWhiteSpace(x.DocumentId) && !string.IsNullOrWhiteSpace(MeasureReport));

        }


        public void UpdateContainedResource(IReportResource reportResource)
        {
            var containedResource = ContainedResources.Where(x => x.DocumentId == reportResource.GetId()).FirstOrDefault();

            if (containedResource == null)
            {
                ContainedResources.Add(new ContainedResource()
                {
                    DocumentId = reportResource.GetId(),
                    IsPatientResource = reportResource.IsPatientResource(),
                    Reference = "" //TODO: Daniel - Need to add
                });
            }
            else
            {
                containedResource.DocumentId = reportResource.GetId();
            }

            ReadyForSubmission = ContainedResources.All(x => !string.IsNullOrWhiteSpace(x.DocumentId) && !string.IsNullOrWhiteSpace(MeasureReport));
        }


        //public void AddContainedResource(Resource resource) 
        //{
        //    var containedResource = ContainedResources.Where(x => x.Reference == resource.TypeName + "/" + resource.Id).FirstOrDefault();

        //    if (containedResource == null)
        //    {
        //        ContainedResources.Add(new ContainedResource
        //        {
        //            Reference = resource.TypeName + "/" + resource.Id,
        //            Resource =  new FhirJsonSerializer().SerializeToString(resource)
        //        }); 
        //    }
        //    else
        //    {
        //        containedResource.Resource =  new FhirJsonSerializer().SerializeToString(resource);
        //    }

        //    ReadyForSubmission = ContainedResources.All(x => !string.IsNullOrWhiteSpace(x.Resource) && !string.IsNullOrWhiteSpace(MeasureReport));
        //}
    }
}
