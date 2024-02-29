using LantanaGroup.Link.Notification.Application.Interfaces;

namespace LantanaGroup.Link.Notification.Domain
{
    public class BaseEntity : IBaseEntity
    {        
        public DateTime CreatedOn { get; set; }
        public DateTime? LastModifiedOn { get; set; }
        public Guid? CreatedBy { get; set; }
    }
}
