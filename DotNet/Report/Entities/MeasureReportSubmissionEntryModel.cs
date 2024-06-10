using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Attributes;
using LantanaGroup.Link.Report.Domain.Enums;
using MongoDB.Bson.Serialization.Attributes;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Report.Application.ResourceCategories;
using MongoDB.Bson;

namespace LantanaGroup.Link.Report.Entities
{

    [BsonCollection("measureReportSubmissionEntry")]
    [BsonIgnoreExtraElements]
    public class MeasureReportSubmissionEntryModel : ReportEntity
    {
        public string FacilityId { get; set; } = string.Empty;
        public string MeasureReportScheduleId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        [BsonSerializer(typeof(MongoFhirBaseSerDes<MeasureReport>))]
        [BsonIgnoreIfNull]
        public MeasureReport? MeasureReport { get; set; }
        public bool ReadyForSubmission { get; private set; } = false;
        public List<ContainedResource> ContainedResources { get; private set; } = new List<ContainedResource>();

        public class ContainedResource
        {
            public string ResourceType { get; set; } = string.Empty;
            public string ResourceId { get; set; } = string.Empty;
            public string DocumentId { get; set; }
            [BsonRepresentation(BsonType.String)]
            public ResourceCategoryType CategoryType { get; set; }

            public string Reference()
            {
                return ResourceType + "/" + ResourceId;
            }
        }

        public void AddMeasureReport(MeasureReport measureReport)
        {
            MeasureReport =  measureReport;

            foreach (var evaluatedResource in measureReport.EvaluatedResource)
            {
                //If the resource is already in the list, skip it
                if (ContainedResources.Any(x => x.Reference() == evaluatedResource.Reference))
                { 
                    continue;
                }

                var reference = evaluatedResource.Reference.Split('/');
                var resourceCategoryType = ResourceCategory.GetResourceCategoryByType(reference[0]);

                if (resourceCategoryType != null)
                {
                    ContainedResources.Add(new ContainedResource
                    {
                        ResourceType = reference[0],
                        ResourceId = reference[1],
                        CategoryType = (ResourceCategoryType)resourceCategoryType
                    });
                }
            }

            ReadyForSubmission = ContainedResources.All(x => !string.IsNullOrWhiteSpace(x.DocumentId) && MeasureReport != null);
        }


        public void UpdateContainedResource(IFacilityResource facilityResource)
        {
            var containedResource = ContainedResources.Where(x => x.ResourceId == facilityResource.GetResource().Id && x.ResourceType == facilityResource.GetResource().TypeName).FirstOrDefault();

            if (containedResource == null)
            {
                var resourceCategoryType = ResourceCategory.GetResourceCategoryByType(facilityResource.GetResource().TypeName);

                ContainedResources.Add(new ContainedResource()
                {
                    DocumentId = facilityResource.GetId(),
                    ResourceId = facilityResource.GetResource().Id,
                    ResourceType = facilityResource.GetResource().TypeName,
                    CategoryType = (ResourceCategoryType)resourceCategoryType
                });
            }
            else
            {
                containedResource.DocumentId = facilityResource.GetId();
            }

            ReadyForSubmission = ContainedResources.All(x => !string.IsNullOrWhiteSpace(x.DocumentId) && MeasureReport != null);
        }
    }
}
