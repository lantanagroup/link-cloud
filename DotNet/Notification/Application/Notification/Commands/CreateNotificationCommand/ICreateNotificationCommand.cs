namespace LantanaGroup.Link.Notification.Application.Notification.Commands
{
    public interface ICreateNotificationCommand
    {
        Task<string> Execute(CreateNotificationModel model, CancellationToken cancellationToken);
    }
}
