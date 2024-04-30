using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

public class BaseEntity : BaseEntityExtended
{
    [Key]
    public Guid Id { get; set; }
}
