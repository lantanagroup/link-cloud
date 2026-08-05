using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Tenant.Entities;

[Table("Vendor")]
public partial class Vendor
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [StringLength(255)]
    [Unicode(false)]
    public string Name { get; set; } = "";

    public VendorAuthenticationSettings? Authentication { get; set; }

    [InverseProperty("Vendor")]
    public virtual ICollection<VendorVersion> VendorVersions { get; set; } = new List<VendorVersion>();
}