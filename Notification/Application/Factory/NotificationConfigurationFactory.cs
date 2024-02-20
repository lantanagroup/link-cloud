using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Infrastructure;
using System.Diagnostics;

namespace LantanaGroup.Link.Notification.Application.Factory
{
    public class NotificationConfigurationFactory : INotificationConfigurationFactory
    {      
        public NotificationConfigurationModel NotificationConfigurationModelCreate(NotificationConfigId id, string facilityId, List<string>? emailAddresses, List<EnabledNotification>? enabledNotifications, List<FacilityChannel>? channels)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Factory - Create NotificationConfigurationModel");

            NotificationConfigurationModel config = new()
            {
                Id = id.Value.ToString(),
                FacilityId = facilityId
            };

            if (emailAddresses is not null)
            {
                if (config.EmailAddresses is not null)
                    config.EmailAddresses.Clear();
                else
                    config.EmailAddresses = new List<string>();

                config.EmailAddresses.AddRange(emailAddresses);
            }

            if (enabledNotifications is not null)
            {
                if (config.EnabledNotifications is not null)
                    config.EnabledNotifications.Clear();
                else
                    config.EnabledNotifications = new List<EnabledNotification>();

                config.EnabledNotifications.AddRange(enabledNotifications);
            }

            if (channels is not null)
            {
                if (config.Channels is not null)
                    config.Channels.Clear();
                else
                    config.Channels = new List<FacilityChannel>();

                config.Channels.AddRange(channels);
            }
           
            return config;
        }

        public CreateFacilityConfigurationModel CreateFacilityConfigurationModelCreate(string facilityId, List<string>? emailAddresses, List<EnabledNotification>? enabledNotifications, List<FacilityChannel>? channels)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Factory - Create FacilityConfigurationModel[CreateCommand]");

            CreateFacilityConfigurationModel config = new()
            {
                FacilityId = facilityId
            };

            if (emailAddresses is not null)
            {
                if (config.EmailAddresses is not null)
                    config.EmailAddresses.Clear();
                else
                    config.EmailAddresses = new List<string>();

                config.EmailAddresses.AddRange(emailAddresses);
            }

            if (enabledNotifications is not null)
            {
                if (config.EnabledNotifications is not null)
                    config.EnabledNotifications.Clear();
                else
                    config.EnabledNotifications = new List<EnabledNotification>();

                config.EnabledNotifications.AddRange(enabledNotifications);
            }

            if (channels is not null)
            {
                if (config.Channels is not null)
                    config.Channels.Clear();
                else
                    config.Channels = new List<FacilityChannel>();

                config.Channels.AddRange(channels);
            }

            return config;
        }

        public UpdateFacilityConfigurationModel UpdateFacilityConfigurationModelCreate(string id,string facilityId, List<string>? emailAddresses, List<EnabledNotification>? enabledNotifications, List<FacilityChannel>? channels)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Factory - Create FacilityConfigurationModel[UpdateCommand]");

            UpdateFacilityConfigurationModel config = new()
            {
                Id = id,
                FacilityId = facilityId
            };

            if (emailAddresses is not null)
            {
                if (config.EmailAddresses is not null)
                    config.EmailAddresses.Clear();
                else
                    config.EmailAddresses = new List<string>();

                config.EmailAddresses.AddRange(emailAddresses);
            }

            if (enabledNotifications is not null)
            {
                if (config.EnabledNotifications is not null)
                    config.EnabledNotifications.Clear();
                else
                    config.EnabledNotifications = new List<EnabledNotification>();

                config.EnabledNotifications.AddRange(enabledNotifications);
            }

            if (channels is not null)
            {
                if (config.Channels is not null)
                    config.Channels.Clear();
                else
                    config.Channels = new List<FacilityChannel>();

                config.Channels.AddRange(channels);
            }

            return config;
        }

        public NotificationConfig NotificationConfigEntityCreate(string facilityId, List<string>? emailAddresses, List<EnabledNotification>? enabledNotifications, List<FacilityChannel>? channels)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Factory - Create NotificationConfigEntity");

            NotificationConfig config = new()
            {
                Id = NotificationConfigId.NewId(),
                FacilityId = facilityId,
                CreatedOn= DateTime.UtcNow
            };

            if (emailAddresses is not null)
            {
                if (config.EmailAddresses is not null)
                    config.EmailAddresses.Clear();
                else
                    config.EmailAddresses = new List<string>();

                config.EmailAddresses.AddRange(emailAddresses);
            }

            if (enabledNotifications is not null)
            {
                if (config.EnabledNotifications is not null)
                    config.EnabledNotifications.Clear();
                else
                    config.EnabledNotifications = new List<EnabledNotification>();

                config.EnabledNotifications.AddRange(enabledNotifications);
            }

            if (channels is not null)
            {
                if (config.Channels is not null)
                    config.Channels.Clear();
                else
                    config.Channels = new List<FacilityChannel>();

                config.Channels.AddRange(channels);
            }

            return config;
        }

        public NotificationConfig NotificationConfigEntityCreate(string id, string facilityId, List<string>? emailAddresses, List<EnabledNotification>? enabledNotifications, List<FacilityChannel>? channels)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Factory - Create NotificationConfigEntity");

            NotificationConfig config = new()
            {
                Id = NotificationConfigId.FromString(id),
                FacilityId = facilityId,
                CreatedOn = DateTime.UtcNow
            };

            if (emailAddresses is not null)
            {
                if (config.EmailAddresses is not null)
                    config.EmailAddresses.Clear();
                else
                    config.EmailAddresses = new List<string>();

                config.EmailAddresses.AddRange(emailAddresses);
            }

            if (enabledNotifications is not null)
            {
                if (config.EnabledNotifications is not null)
                    config.EnabledNotifications.Clear();
                else
                    config.EnabledNotifications = new List<EnabledNotification>();

                config.EnabledNotifications.AddRange(enabledNotifications);
            }

            if (channels is not null)
            {
                if (config.Channels is not null)
                    config.Channels.Clear();
                else
                    config.Channels = new List<FacilityChannel>();

                config.Channels.AddRange(channels);
            }

            return config;
        }

        public NotificationConfigurationSearchRecord CreateNotificationConfigurationSearchRecord(string? searchText, string? filterFacilityBy, string? sortBy, int pageSize, int pageNumber)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Factory - Create NotificationConfigurationSearchRecord");

            NotificationConfigurationSearchRecord model = new()
            {
                SearchText = searchText,
                FilterFacilityBy = filterFacilityBy,
                SortBy = sortBy,
                PageSize = pageSize,
                PageNumber = pageNumber
            };

            return model;
        }
    }
}
