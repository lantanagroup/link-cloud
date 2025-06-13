using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.ResourceCategories;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Report.Entities
{
    public enum ValidationStatus
    {
        Pending,
        Passed,
        Failed,
        Requested
    }

    [BsonCollection("measureReportSubmissionEntry")]
    [BsonIgnoreExtraElements]
    public class MeasureReportSubmissionEntryModel : BaseEntityExtended
    {
        public string FacilityId { get; set; } = string.Empty;
        public string ReportScheduleId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        [BsonSerializer(typeof(MongoFhirBaseSerDes<MeasureReport>))]
        [BsonIgnoreIfNull]
        public MeasureReport? MeasureReport { get; set; }
        public string? PayloadUri { get; set; }

        public PatientSubmissionStatus Status { get; set; } = PatientSubmissionStatus.PendingEvaluation;
        public ValidationStatus ValidationStatus { get; set; } = ValidationStatus.Pending;
        public List<ContainedResource> ContainedResources { get; set; } = new List<ContainedResource>();

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
            MeasureReport = measureReport;

            if (Status != PatientSubmissionStatus.NotReportable)
            {
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
                
                UpdateStatus();
            }
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

            UpdateStatus();
        }

        private void UpdateStatus()
        {
            Status = ContainedResources.All(x => !string.IsNullOrWhiteSpace(x.DocumentId) && MeasureReport != null) ? PatientSubmissionStatus.ReadyForValidation : Status;
        }
    }
}
