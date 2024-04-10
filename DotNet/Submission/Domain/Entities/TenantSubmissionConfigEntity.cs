using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using LantanaGroup.Link.Submission.Application.Models.ApiModels;

namespace LantanaGroup.Link.Submission.Domain.Entities
{
    [BsonCollection("tenantSubmissionConfig")]
    public class TenantSubmissionConfigEntity : BaseEntity
    {
        public string FacilityId { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public List<Method> Methods { get; set; } = new List<Method>();
        public DateTime? CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }

        public TenantSubmissionConfigEntity()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString();
            }
        }
    }
}
