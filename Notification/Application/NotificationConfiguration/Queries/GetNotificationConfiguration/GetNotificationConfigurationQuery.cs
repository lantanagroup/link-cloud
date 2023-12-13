using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;
using System.Diagnostics;

namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries
{
    public class GetNotificationConfigurationQuery : IGetNotificationConfigurationQuery
    {
        private readonly ILogger<GetNotificationConfigurationQuery> _logger;
        private readonly INotificationConfigurationRepository _datastore;
        private readonly INotificationConfigurationFactory _notificationConfigurationFactory;

        public GetNotificationConfigurationQuery(ILogger<GetNotificationConfigurationQuery> logger, INotificationConfigurationRepository datastore, INotificationConfigurationFactory notificationConfigurationFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
            _notificationConfigurationFactory = notificationConfigurationFactory ?? throw new ArgumentNullException(nameof(notificationConfigurationFactory));
        }

        public async Task<NotificationConfigurationModel> Execute(string id)
        {
            if (string.IsNullOrEmpty(id)) { throw new ArgumentNullException(nameof(id)); }

            try
            {
                NotificationConfigurationModel? config = null;

                var result = await _datastore.GetAsync(id);

                if (result != null)
                {
                    config = new NotificationConfigurationModel();
                    config = _notificationConfigurationFactory.NotificationConfigurationModelCreate(result.Id, result.FacilityId, result.EmailAddresses, result.EnabledNotifications, result.Channels);
                }


                return config;
            }
            catch (ArgumentNullException ex)
            {
                _logger.LogError(new EventId(NotificationLoggingIds.GetItem, "Notification Service - Get notification configuration by id"), ex, "Failed to get notification configuration, no id provided.");
                var currentActivity = Activity.Current;
                currentActivity?.SetStatus(ActivityStatusCode.Error, $"Failed to get notification configuration, no id provided.");
                throw;
            }
        }
    }
}
