using Census.Domain.Entities;

namespace LantanaGroup.Link.Census.Models
{
    public class CensusConfigModel
    {
        public Guid Id { get; set; }
        public string FacilityId { get; set; }
        public string ScheduledTrigger { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }

        public static CensusConfigModel FromDomain(CensusConfigEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            return new CensusConfigModel
            {
                Id = entity.Id,
                FacilityId = entity.FacilityID,
                ScheduledTrigger = entity.ScheduledTrigger,
                CreateDate = entity.CreateDate,
                ModifyDate = entity.ModifyDate
            };
        }
    }
}
