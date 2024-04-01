using System.ComponentModel.DataAnnotations;


namespace LantanaGroup.Link.Shared.Domain.Entities
{
    public class SqlBaseEntity
    {
        [Key]
        public Guid Id { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? LastModifiedOn { get; set; }
    }
}
