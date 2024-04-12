namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface IEmailService
    {
        Task<bool> Send(string id, List<string> to, List<string>? bcc, string subject, string message, string? from = null);
    }
}
