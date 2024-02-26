using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using System.Diagnostics;
using LantanaGroup.Link.Notification.Domain.Entities;

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

        public async Task<NotificationConfigurationModel> Execute(NotificationConfigId id, CancellationToken cancellationToken)
        {     
            try
            {
                var config = await _datastore.GetAsync(id, true, cancellationToken);

                if (config is null)
                {
                    return null;                    
                }

                var configModel = _notificationConfigurationFactory.NotificationConfigurationModelCreate(config.Id, config.FacilityId, config.EmailAddresses, config.EnabledNotifications, config.Channels);
                return configModel;
            }
            catch (ArgumentNullException)
            {                
                Activity.Current?.SetStatus(ActivityStatusCode.Error);               
                throw;
            }
        }
    }
}
