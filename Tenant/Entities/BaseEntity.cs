
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.Tenant.Entities
{
    public class BaseEntity 
    {
        [Key]
        public Guid Id { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? LastModifiedOn { get; set; }
    }
}
