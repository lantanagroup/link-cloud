using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Tenant.Business.Models
{
    public class FacilitySearchModel
    {
        public string? FacilityId { get; set; }
        public string? FacilityName { get; set; }
        public Guid? Id { get; set; }
        public bool? FacilityNameContains { get; set; }
        public string? TimeZone { get; set; }
        public VendorModel? Vendor { get; set; }

        public bool? IsDeleted { get; set; }
    }
}