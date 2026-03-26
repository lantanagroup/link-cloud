using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;
using LantanaGroup.Link.Account.Application.Interfaces.Domain;

namespace LantanaGroup.Link.Account.Domain.Entities
{
    [Table("Roles")]
    public class LinkRole : IBaseEntity
    {
        /// <summary>
        /// Initializes a new instance of <see cref="LinkRole"/>.
        /// </summary>
        /// <remarks>
        /// The Id property is initialized to form a new GUID string value.
        /// </remarks>
        public LinkRole()
        {
            Id = Guid.NewGuid();
        }

        /// <summary>
        /// Initializes a new instance of <see cref="LinkRole"/>.
        /// </summary>
        /// <param name="name">The role name.</param>
        /// <remarks>
        /// The Id property is initialized to form a new GUID string value.
        /// </remarks>
        public LinkRole(string name) : this()
        {
            Name = name;
        }

        [MaxLength(36)]
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? LastModifiedOn { get; set; }
        public string? LastModifiedBy { get; set; }
      
        public virtual ICollection<LinkUserRole> UserRoles { get; set; } = [];
        public virtual ICollection<LinkRoleClaim> RoleClaims { get; set; } = [];

        public override string ToString()
        {
            return Name ?? string.Empty;
        }
    }    

    [Table("RoleClaims")]
    public class LinkRoleClaim
    {
        public int Id { get; set; } = default!;
        [MaxLength(36)]
        public Guid RoleId { get; set; } = default!;
        public string? ClaimType { get; set; }
        public string? ClaimValue { get; set; }

        public virtual LinkRole Role { get; set; } = new LinkRole();

        public virtual Claim ToClaim()
        {
            return new(ClaimType!, ClaimValue!);
        }

        public virtual void InitializeFromClaim(Claim claim)
        {
            ClaimType = claim.Type;
            ClaimValue = claim.Value;
        }
    }
}
