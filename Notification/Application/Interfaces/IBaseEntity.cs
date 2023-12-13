namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface IBaseEntity
    {
        string Id { get; set; }
        DateTime CreatedOn { get; set; }
        Guid? CreatedBy { get; set; }
    }
}
