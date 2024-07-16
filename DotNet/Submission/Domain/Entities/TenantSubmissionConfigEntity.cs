using LantanaGroup.Link.Shared.Domain.Entities;
using LantanaGroup.Link.Submission.Application.Models.ApiModels;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Submission.Domain.Entities
{

    //[BsonCollection("tenantSubmissionConfig")]
    [Table("TenantSubmissionConfigs")]
    public class TenantSubmissionConfigEntity : BaseEntityExtended
    {
        public string FacilityId { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public List<Method> Methods { get; set; } = new List<Method>();
    }
}
