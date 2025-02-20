using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Shared.Application.Models
{
    public class PatientSubmissionModel : BaseEntityExtended
    {
        public string FacilityId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string ReportScheduleId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PatientResources { get; set; }
        public string OtherResources { get; set; }
    }
}
