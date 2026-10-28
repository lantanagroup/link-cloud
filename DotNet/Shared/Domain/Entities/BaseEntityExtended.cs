namespace LantanaGroup.Link.Shared.Domain.Entities;

public class BaseEntityExtended : BaseEntity
{
    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
}
