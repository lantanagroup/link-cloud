using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Infrastructure;
using System.Diagnostics;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;

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

        public async Task<NotificationConfigurationModel> Execute(string facilityId)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Get Facility Configuration By Facility Id Query");

            if (string.IsNullOrEmpty(facilityId)) { throw new ArgumentNullException("Failed to get a notification configuration by facility id, no facility id was provided."); }

            try 
            {
                NotificationConfigurationModel? config = null;

                var result = await _datastore.GetNotificationConfigByFacilityAsync(facilityId);

                if(result != null)
                {
                    config = new NotificationConfigurationModel();
                    config = _notificationConfigurationFactory.NotificationConfigurationModelCreate(result.Id, result.FacilityId, result.EmailAddresses, result.EnabledNotifications, result.Channels);
                }
                

                return config;
            }
            catch(ArgumentNullException ex)
            {
                _logger.LogWarning(new EventId(NotificationLoggingIds.GetItem, "Notification Service - Get notification configuration by facility id"), ex, ex.Message);
                throw;
            }            
        }
    }
}
