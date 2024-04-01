using System.ComponentModel.DataAnnotations;


namespace LantanaGroup.Link.Shared.Domain.Entities
{
    public class SqlBaseEntity
    {
        [Key]
        public string Id { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? LastModifiedOn { get; set; }
    }
}
