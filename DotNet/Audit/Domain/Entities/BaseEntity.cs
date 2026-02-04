using LantanaGroup.Link.Audit.Application.Interfaces;

namespace LantanaGroup.Link.Audit.Domain.Entities
{
    public class BaseEntity : IBaseEntity
    {     
        public DateTime CreatedOn { get; set; }
        public Guid? CreatedBy { get; set; }
    }
}
