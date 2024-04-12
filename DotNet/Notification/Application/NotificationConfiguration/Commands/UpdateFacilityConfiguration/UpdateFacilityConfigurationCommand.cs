using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Notification.Infrastructure.Logging;
using System.Diagnostics;

namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands
{
    public class UpdateFacilityConfigurationCommand : IUpdateFacilityConfigurationCommand
    {
        private readonly ILogger<GetFacilityConfigurationQuery> _logger;
        private readonly INotificationConfigurationRepository _datastore;
        private readonly INotificationConfigurationFactory _notificationConfigurationFactory;

        public UpdateFacilityConfigurationCommand(ILogger<GetFacilityConfigurationQuery> logger, INotificationConfigurationRepository datastore, INotificationConfigurationFactory notificationConfigurationFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));           
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
            _notificationConfigurationFactory = notificationConfigurationFactory ?? throw new ArgumentNullException(nameof(notificationConfigurationFactory));
        }

        public async Task<bool> Execute(UpdateFacilityConfigurationModel model, CancellationToken cancellationToken)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Update Facility Configuration");

            if (string.IsNullOrEmpty(model.Id)) { throw new ArgumentNullException(nameof(model.Id));  }
            if (string.IsNullOrEmpty(model.FacilityId)) { throw new ArgumentNullException(nameof(model.FacilityId)); }

            try
            {               
                NotificationConfig entity = _notificationConfigurationFactory.NotificationConfigEntityCreate(model.Id, model.FacilityId, model.EmailAddresses, model.EnabledNotifications, model.Channels);
                bool outcome = await _datastore.UpdateAsync(entity, cancellationToken);              

                _logger.LogNotificationConfigurationUpdate(model.Id, model);
                return outcome;

            }
            catch (Exception)
            {                 
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
        }
    }
}
