using LantanaGroup.Link.Account.Application.Interfaces.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Domain.Entities
{

    [Table("Users")]
    public class LinkUser : IBaseEntity
    {
        [MaxLength(36)]
        public Guid Id { get; set; }
        public string? UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;     
        public DateTime? LastSeen { get; set; }
        public DateTime CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? LastModifiedOn { get; set; }
        public string? LastModifiedBy { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }


        public virtual ICollection<LinkUserClaim> Claims { get; set; } = [];
        public virtual ICollection<LinkUserRole> UserRoles { get; set; } = [];        
    }

    [Table("UserRoles")]
    public class LinkUserRole
    {
        [MaxLength(36)]
        public Guid UserId { get; set; } = default!;
        [MaxLength(36)]
        public Guid RoleId { get; set; } = default!;

        public virtual LinkUser User { get; set; } = default!;
        public virtual LinkRole Role { get; set; } = default!;
    }

    [Table("UserClaims")]
    public class LinkUserClaim
    {
        public int Id { get; set; }
        [MaxLength(36)]
        public Guid UserId { get; set; }
        public string? ClaimType { get; set; }
        public string? ClaimValue { get; set; }     

        public virtual Claim ToClaim()
        {
            return new(ClaimType!, ClaimValue!);
        }

        public virtual void InitializeFromClaim(Claim claim)
        {
            ClaimType = claim.Type;
            ClaimValue = claim.Value;
        }

        public virtual LinkUser User { get; set; } = default!;
    }
}
