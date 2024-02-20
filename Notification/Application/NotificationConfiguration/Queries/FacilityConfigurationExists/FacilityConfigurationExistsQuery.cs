using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Infrastructure;
using System.Diagnostics;

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

        public async Task<bool> Execute(NotificationConfigId id)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Check if Facility Configuration Exists Query");           

            try 
            {
                bool exists = await _datastore.Get(id) is not null;
                return exists;
            }
            catch(ArgumentNullException)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
            
        }
    }
}
