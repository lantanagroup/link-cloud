using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;

[Table("Users")]
public class NhsnUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(128)]
    public string ExternalUserId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? GroupsRaw { get; set; }

    [MaxLength(64)]
    public string? FacilityId { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    [MaxLength(256)]
    public string? CreatedBy { get; set; }

    public DateTime? LastModifiedOn { get; set; }

    public DateTime? LastAccessedOn { get; set; }

    [MaxLength(256)]
    public string? LastModifiedBy { get; set; }
}