using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Domain.Entities;

namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface INotificationFactory
    {
        public CreateNotificationModel CreateNotificationModelCreate(string notificationType, string? facilityId, string? correlationId, string subject, string body, List<string> recipients, List<string>? bcc);

        public NotificationEntity NotificationEntityCreate(string notificationType, string? facilityId, string? correlationId, string subject, string body, List<string> recipients, List<string>? bcc);

        public NotificationModel NotificationModelCreate(NotificationId id, string notificationType, string? facilityId, string? correlationId, string subject, string body, List<string> recipients, List<string>? bcc, DateTime createdOn, List<DateTime> sentOn);

        public SendNotificationModel CreateSendNotificationModel(string id, List<string> recipients, List<string>? bcc, string subject, string message);

        NotificationSearchRecord CreateNotificationSearchRecord(string? searchText, string? filterFacilityBy, string? filterNotificationType, string? sortBy, int pageSize, int pageNumber);
    }
}
