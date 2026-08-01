using LantanaGroup.Link.Shared.Application.Enums;

namespace LantanaGroup.Link.Tenant.Business.Models
{
    public class FacilitySearchModel
    {
        public string? FacilityId { get; set; }
        public string? FacilityName { get; set; }
        public Guid? Id { get; set; }

        /// <summary>
        /// Treats <see cref="FacilityName"/> as a free-text fragment matched against both the
        /// facility name and the facility id, rather than as an exact name. This is what the
        /// admin UI's single search box needs: users type part of a name or an id and expect
        /// either to match. Leave unset for an exact name lookup.
        /// </summary>
        public bool? PartialMatch { get; set; }
        public string? TimeZone { get; set; }
        public Vendor? Vendor { get; set; }

        public bool? IsDeleted { get; set; }
    }
}