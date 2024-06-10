using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using LantanaGroup.Link.Submission.Application.Models.ApiModels;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Submission.Domain.Entities
{
    public readonly record struct TenantSubmissionConfigEntityId(Guid Value)
    {
        public static TenantSubmissionConfigEntityId Empty => new(Guid.Empty);
        public static TenantSubmissionConfigEntityId NewId() => new(Guid.NewGuid());
        public static TenantSubmissionConfigEntityId FromString(string id) => new(new Guid(id));
    }

    //[BsonCollection("tenantSubmissionConfig")]
    [Table("TenantSubmissionConfigs")]
    public class TenantSubmissionConfigEntity : BaseEntity
    {
        public new TenantSubmissionConfigEntityId Id { get; set; }
        public string FacilityId { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public List<Method> Methods { get; set; } = new List<Method>();
        public DateTime? CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }
    }
}
