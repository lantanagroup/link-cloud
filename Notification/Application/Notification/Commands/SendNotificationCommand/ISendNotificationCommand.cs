namespace LantanaGroup.Link.Notification.Application.Notification.Commands
{
    public interface ISendNotificationCommand
    {
        Task<bool> Execute(SendNotificationModel model);
    }
}
