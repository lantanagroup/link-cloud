using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Shared.Application.Models;
using System.Diagnostics;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;

namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands
{
    public class UpdateFacilityConfigurationCommand : IUpdateFacilityConfigurationCommand
    {
        private readonly ILogger<GetFacilityConfigurationQuery> _logger;
        private readonly INotificationConfigurationRepository _datastore;
        private readonly INotificationConfigurationFactory _notificationConfigurationFactory;
        private readonly IAuditEventFactory _auditEventFactory;
        private readonly ICreateAuditEventCommand _createAuditEventCommand;

        public UpdateFacilityConfigurationCommand(ILogger<GetFacilityConfigurationQuery> logger, IAuditEventFactory auditEventFactory, ICreateAuditEventCommand createAuditEventCommand, IKafkaProducerFactory kafkaProducerFactory, INotificationConfigurationRepository datastore, INotificationConfigurationFactory notificationConfigurationFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditEventFactory = auditEventFactory ?? throw new ArgumentNullException(nameof(auditEventFactory));
            _createAuditEventCommand = createAuditEventCommand ?? throw new ArgumentNullException(nameof(createAuditEventCommand));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
            _notificationConfigurationFactory = notificationConfigurationFactory ?? throw new ArgumentNullException(nameof(notificationConfigurationFactory));
        }

        public async Task<string> Execute(UpdateFacilityConfigurationModel model)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Update Facility Configuration");

            if (string.IsNullOrEmpty(model.Id)) { throw new ArgumentNullException(nameof(model.Id));  }
            if (string.IsNullOrEmpty(model.FacilityId)) { throw new ArgumentNullException(nameof(model.FacilityId)); }

            try
            {               
                NotificationConfig entity = _notificationConfigurationFactory.NotificationConfigEntityCreate(model.Id, model.FacilityId, model.EmailAddresses, model.EnabledNotifications, model.Channels);
                bool outcome = await _datastore.UpdateAsync(entity);                

                //TODO: Get user info
                //Create audit event
                string notes = $"Updated notification configuration ({entity.Id}) for '{entity.FacilityId}'.";
                AuditEventMessage auditEventMessage = _auditEventFactory.CreateAuditEvent(null, null, "SystemUser", AuditEventType.Update, typeof(NotificationConfig).Name, notes);
                _ = Task.Run(() => _createAuditEventCommand.Execute(model.FacilityId, auditEventMessage));

                //Log update of existing notification configuration                        
                _logger.LogInformation(new EventId(NotificationLoggingIds.UpdateItem, "Notification Service - Update notification configuration"), "Notification configuration '{id}' updated for '{facilityId}'.", entity.Id, entity.FacilityId);
                return entity.Id;

            }
            catch (Exception ex)
            {
                ex.Data.Add("facility id", model.FacilityId);
                _logger.LogError(new EventId(NotificationLoggingIds.UpdateItem, "Notification Service - Update notification configuration"), ex, "Failed to update notification configuration for facility {facilityId}.", model.FacilityId);
                var currentActivity = Activity.Current;
                currentActivity?.SetStatus(ActivityStatusCode.Error, $"Failed to updated notification configuration for facility {model.FacilityId}.");
                throw;
            }
        }
    }
}
