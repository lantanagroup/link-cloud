using LantanaGroup.Link.Report.Attributes;

namespace LantanaGroup.Link.Report.Entities
{
    [BsonCollection("sharedResource")]
    public class SharedResourceModel
    {
        public string FacilityId { get; set; }
        public string Reference { get; set; }
        public string Resource { get; set; }
    }
}
