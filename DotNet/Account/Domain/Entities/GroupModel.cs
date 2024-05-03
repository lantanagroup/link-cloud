using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using LantanaGroup.Link.Account.Domain.Interfaces;

namespace LantanaGroup.Link.Account.Domain.Entities
{
    [Table("Groups")]
    public class GroupModel : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        public List<string>? FacilityIds { get; set; }
        
        public ICollection<LinkUser> Accounts { get; set; } = new List<LinkUser>();
        public ICollection<LinkRole> Roles { get; set; } = new List<LinkRole>();

    }
}
