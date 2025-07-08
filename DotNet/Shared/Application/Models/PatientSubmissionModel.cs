namespace LantanaGroup.Link.Shared.Application.Models
{
    public class PatientSubmissionModel
    {
        public string FacilityId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string ReportScheduleId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PatientResources { get; set; } = string.Empty;
        public string OtherResources { get; set; } = string.Empty;
    }
}
