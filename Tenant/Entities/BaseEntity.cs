
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.Tenant.Entities
{
    public class BaseEntity : Shared.Domain.Entities.SqlBaseEntity
    {
        [Key]
        public new Guid Id { get; set; }
    }
}
