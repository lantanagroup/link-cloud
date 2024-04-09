namespace LantanaGroup.Link.Account.Domain.Interfaces
{
    public interface IBaseEntity
    {
        Guid Id { get; set; }

        DateTime CreatedOn { get; set; }
        Guid? CreatedBy { get; set; }
        DateTime? LastModifiedOn { get; set; }
        Guid? LastModifiedBy { get; set; }
        bool IsDeleted { get; set; }
    }
}
