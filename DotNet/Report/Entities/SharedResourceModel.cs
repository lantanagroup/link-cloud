using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Attributes;

namespace LantanaGroup.Link.Report.Entities
{
    [BsonCollection("sharedResource")]
    public class SharedResourceModel : ReportEntity, IReportResource
    {
        public string FacilityId { get; set; }
        public string ResourceId { get; set; }
        public string ResourceType { get; set; }
        public string Resource { get; set; }

        public string GetId()
        {
            return this.Id;
        }

        public bool IsPatientResource()
        {
            return false;
        }
    }
}
