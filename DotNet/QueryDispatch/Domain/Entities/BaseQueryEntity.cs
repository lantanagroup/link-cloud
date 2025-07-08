using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.QueryDispatch.Domain.Entities
{
    public class BaseQueryEntity : BaseEntityExtended
    {
        public string FacilityId { get; set; } = string.Empty;

        // Explicitly hide base class properties
        public new DateTime CreateDate { get; set; }
        public new DateTime? ModifyDate { get; set; }
    }
}
