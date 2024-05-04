namespace LantanaGroup.Link.Account.Application.Interfaces.Domain
{
    public interface IBaseEntity
    {
        DateTime CreatedOn { get; set; }
        Guid? CreatedBy { get; set; }
        DateTime? LastModifiedOn { get; set; }
        Guid? LastModifiedBy { get; set; }
        bool IsDeleted { get; set; }
    }
}
