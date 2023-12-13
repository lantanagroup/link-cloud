using LantanaGroup.Link.Notification.Application.Interfaces;
using MongoDB.Bson.Serialization.Attributes;

namespace LantanaGroup.Link.Notification.Domain.Entities
{
    public class BaseEntity : IBaseEntity
    {
        [BsonId]
        public string Id { get; set; }
        public DateTime CreatedOn { get; set; }
        public Guid? CreatedBy { get; set; }
    }
}
