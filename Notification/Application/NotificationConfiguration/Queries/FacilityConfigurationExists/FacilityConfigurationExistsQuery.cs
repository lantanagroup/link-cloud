using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Infrastructure;
using System.Diagnostics;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;

namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries
{
    public class FacilityConfigurationExistsQuery : IFacilityConfigurationExistsQuery
    {
        private readonly ILogger<GetFacilityConfigurationQuery> _logger;     
        private readonly INotificationConfigurationRepository _datastore;
       
        public FacilityConfigurationExistsQuery(ILogger<GetFacilityConfigurationQuery> logger, INotificationConfigurationRepository datastore)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
        }

        public async Task<bool> Execute(string id)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Check if Facility Configuration Exists Query");

            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException("Failed to determine if a notification configuration exists, no id was provided.");

            try 
            {
                bool exists = await _datastore.ExistsAsync(id);

                return exists;
            }
            catch(ArgumentNullException ex)
            {
                _logger.LogWarning(new EventId(NotificationLoggingIds.UpdateItemNotFound, "Notification Service - Notification configuration exists"), ex, ex.Message);
                throw;
            }
            
        }
    }
}
