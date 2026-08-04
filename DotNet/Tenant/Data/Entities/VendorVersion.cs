using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Tenant.Entities;

[Table("VendorVersion")]
public partial class VendorVersion
{
    [Key]
    public Guid Id { get; set; }

    public Guid VendorId { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    public string Version { get; set; } = "";

    [ForeignKey("VendorId")]
    [InverseProperty("VendorVersions")]
    public virtual Vendor? Vendor { get; set; }
}