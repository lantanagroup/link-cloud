using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands;
using LantanaGroup.Link.Notification.Domain.Entities;

namespace LantanaGroup.Link.Notification.Application.Interfaces
{
    public interface INotificationConfigurationFactory
    {
        public NotificationConfigurationModel NotificationConfigurationModelCreate(NotificationConfigId id, string facilityId, List<string>? emailAddresses, List<EnabledNotification>? enabledNotifications, List<FacilityChannel>? channels);

        public CreateFacilityConfigurationModel CreateFacilityConfigurationModelCreate(string facilityId, List<string>? emailAddresses, List<EnabledNotification>? enabledNotifications, List<FacilityChannel>? channels);

        public UpdateFacilityConfigurationModel UpdateFacilityConfigurationModelCreate(string id, string facilityId, List<string>? emailAddresses, List<EnabledNotification>? enabledNotifications, List<FacilityChannel>? channels);

        public NotificationConfig NotificationConfigEntityCreate(string facilityId, List<string>? emailAddresses, List<EnabledNotification>? enabledNotifications, List<FacilityChannel>? channels);

        public NotificationConfig NotificationConfigEntityCreate(string id, string facilityId, List<string>? emailAddresses, List<EnabledNotification>? enabledNotifications, List<FacilityChannel>? channels);

        NotificationConfigurationSearchRecord CreateNotificationConfigurationSearchRecord(string? searchText, string? filterFacilityBy, string? sortBy, int pageSize, int pageNumber);
    }
}
