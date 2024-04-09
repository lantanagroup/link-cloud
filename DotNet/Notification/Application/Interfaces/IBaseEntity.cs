namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface IBaseEntity
    {
        DateTime CreatedOn { get; set; }
        DateTime? LastModifiedOn { get; set; }
        Guid? CreatedBy { get; set; }
    }
}
