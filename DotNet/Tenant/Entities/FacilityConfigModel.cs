using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.Tenant.Entities
{

    public class FacilityConfigModel : BaseEntityExtended
    {
        public string FacilityId { get; set; } = null!;
        public string? FacilityName { get; set; }
        public ScheduledReportModel ScheduledReports { get; set; } = null!;

        public FacilityConfigModel ShallowCopy()
        {
            return (FacilityConfigModel)this.MemberwiseClone();
        }
    }
}
