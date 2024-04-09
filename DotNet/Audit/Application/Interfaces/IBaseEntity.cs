namespace LantanaGroup.Link.Audit.Application.Interfaces
{
    public interface IBaseEntity
    {
        DateTime CreatedOn { get; set; }
        Guid? CreatedBy { get; set; }
    }
}
