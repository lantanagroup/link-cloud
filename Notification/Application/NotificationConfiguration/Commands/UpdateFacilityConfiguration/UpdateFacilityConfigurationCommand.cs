using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Notification.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models;
using System.Diagnostics;

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

                _logger.LogNotificationConfigurationUpdate(model.Id, model);
                return entity.Id.Value.ToString();

            }
            catch (Exception ex)
            {                 
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
        }
    }
}
