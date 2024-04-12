namespace LantanaGroup.Link.Notification.Application.Notification.Commands
{
    public interface IValidateEmailAddressCommand
    {
        Task<bool> Execute(string emailAddress);
    }
}
