namespace LantanaGroup.Link.Submission.Application.Models.ApiModels
{
    public class TenantSubmissionConfig
    {
        public string? Id { get; set; }
        public string FacilityId { get; set; }
        public string ReportType { get; set; }
        public List<Method> Methods { get; set; }
        public DateTime? CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }
    }
}