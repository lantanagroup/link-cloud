using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.Census.Domain.Entities;

public class BaseEntity : BaseEntityExtended
{
    [Key]
    public new Guid Id { get; set; }
}
