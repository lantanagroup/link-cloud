using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.QueryDispatch.Domain.Entities
{
    public class BaseQueryEntity : BaseEntity
    {
        public string FacilityId { get; set; }
    }
}
