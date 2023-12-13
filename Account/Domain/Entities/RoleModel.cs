using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using LantanaGroup.Link.Account.Domain.Interfaces;
using LantanaGroup.Link.Account.Domain.Enums;

namespace LantanaGroup.Link.Account.Domain.Entities
{

    [Table("Roles")]
    public class RoleModel : BaseEntity
    {
        
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public PermissionTypes Permissions { get; set; }

        public List<string>? FacilityIds { get; set; }

        public ICollection<AccountModel> Accounts { get; set; } = new List<AccountModel>();
        public ICollection<GroupModel> Groups { get; set; } = new List<GroupModel>();
                
    }
}
