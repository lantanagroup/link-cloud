using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;

[Table("Facilities")]
public class NhsnFacility
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string FacilityId { get; set; } = string.Empty;

    public bool IsOnboarded { get; set; }
}