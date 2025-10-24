using LantanaGroup.Link.Census.Domain.Entities;

namespace LantanaGroup.Link.Census.Models
{
    public class CensusPatientListModel
    {
        public string FacilityId { get; set; }
        public string PatientId { get; set; }
        public string? DisplayName { get; set; }
        public DateTime? AdmitDate { get; set; }
        public bool IsDischarged { get; set; }
        public DateTime? DischargeDate { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }

        public static CensusPatientListModel FromDomain(CensusPatientListEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            return new CensusPatientListModel
            {
                FacilityId = entity.FacilityId,
                PatientId = entity.PatientId,
                DisplayName = entity.DisplayName,
                AdmitDate = entity.AdmitDate,
                IsDischarged = entity.IsDischarged,
                DischargeDate = entity.DischargeDate,
                CreateDate = entity.CreateDate,
                ModifyDate = entity.ModifyDate
            };
        }
    }
}
