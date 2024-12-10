using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Tenant.Entities
{

    [Table("Facilities")]
    public class FacilityConfigModel : BaseEntityExtended
    {
        public string FacilityId { get; set; } = null!;
        public string? FacilityName { get; set; }
        public string TimeZone { get; set; }
        public ScheduledReportModel ScheduledReports { get; set; } = null!;

        public FacilityConfigModel ShallowCopy()
        {
            return (FacilityConfigModel)this.MemberwiseClone();
        }
    }
}
