using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;

[Table("UserRoles")]
public class NhsnUserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }

    public NhsnUser User { get; set; } = default!;
    public NhsnRole Role { get; set; } = default!;
}