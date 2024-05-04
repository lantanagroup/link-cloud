using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using LantanaGroup.Link.Account.Application.Interfaces.Domain;

namespace LantanaGroup.Link.Account.Domain.Entities
{
    public class BaseEntity : IBaseEntity
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }


        public DateTime CreatedOn { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? LastModifiedOn { get; set; }
        public Guid? LastModifiedBy { get; set; }
        public bool IsDeleted { get; set; }

    }
}
