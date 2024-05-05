using System.ComponentModel.DataAnnotations.Schema;
using LantanaGroup.Link.Account.Application.Interfaces.Domain;
using Microsoft.AspNetCore.Identity;

namespace LantanaGroup.Link.Account.Domain.Entities
{
    [Table("Roles")]
    public class LinkRole : IdentityRole, IBaseEntity
    {        
        public string? Description { get; set; }
        public virtual ICollection<LinkUserRole> UserRoles { get; set; } = new List<LinkUserRole>();
        public virtual ICollection<LinkRoleClaim> RoleClaims { get; set; } = new List<LinkRoleClaim>();
        public DateTime CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? LastModifiedOn { get; set; }
        public string? LastModifiedBy { get; set; }
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
