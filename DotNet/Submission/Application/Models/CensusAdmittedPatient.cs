namespace LantanaGroup.Link.Submission.Application.Models
{
    public class CensusAdmittedPatient
    {
        public string FacilityId { get; set; }
        public string PatientId { get; set; }
        public string? DisplayName { get; set; }
        public DateTime? AdmitDate { get; set; }
        public bool IsDischarged { get; set; }
        public DateTime? DischargeDate { get; set; }
    }
}