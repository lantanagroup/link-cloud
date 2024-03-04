using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Infrastructure;
using System.Diagnostics;

namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries
{
    public class GetFacilityConfigurationQuery : IGetFacilityConfigurationQuery
    {
        private readonly ILogger<GetFacilityConfigurationQuery> _logger;
        private readonly INotificationConfigurationRepository _datastore;
        private readonly INotificationConfigurationFactory _notificationConfigurationFactory;

        public GetFacilityConfigurationQuery(ILogger<GetFacilityConfigurationQuery> logger, INotificationConfigurationRepository datastore, INotificationConfigurationFactory notificationConfigurationFactory) 
        { 
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
            _notificationConfigurationFactory = notificationConfigurationFactory ?? throw new ArgumentNullException(nameof(notificationConfigurationFactory));
        }

        public async Task<NotificationConfigurationModel> Execute(string facilityId, CancellationToken cancellationToken)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facility Configuration By Facility Id Query");

            if (string.IsNullOrEmpty(facilityId)) 
            { 
                throw new ArgumentNullException("Failed to get a notification configuration by facility id, no facility id was provided."); 
            }

            try 
            {             
                var config = await _datastore.GetFacilityNotificationConfigAsync(facilityId, true, cancellationToken);
                if (config is null)
                {
                    return null;
                }

                var configModel = _notificationConfigurationFactory.NotificationConfigurationModelCreate(config.Id, config.FacilityId, config.EmailAddresses, config.EnabledNotifications, config.Channels);
                return configModel;
            }
            catch(ArgumentNullException)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }            
        }
    }
}
