using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.QueryDispatch.Domain.Entities
{
    public class BaseQueryEntity : BaseEntityExtended
    {
        public string FacilityId { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }
    }
}
