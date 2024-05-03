using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace LantanaGroup.Link.Account.Domain.Entities
{
    [Table("Roles")]
    public class LinkRole : IdentityRole
    {        
        public string? Description { get; set; }
        public virtual ICollection<LinkUserRole> UserRoles { get; set; } = new List<LinkUserRole>();
        public virtual ICollection<LinkRoleClaim> RoleClaims { get; set; } = new List<LinkRoleClaim>();
    }

    [Table("AccountRoles")]
    public class LinkUserRole : IdentityUserRole<string>
    {
        public virtual LinkUser User { get; set; } = default!;
        public virtual LinkRole Role { get; set; } = default!;
    }

    [Table("RoleClaims")]
    public class LinkRoleClaim : IdentityRoleClaim<string>
    {
        public virtual LinkRole Role { get; set; } = default!;
    }
}
