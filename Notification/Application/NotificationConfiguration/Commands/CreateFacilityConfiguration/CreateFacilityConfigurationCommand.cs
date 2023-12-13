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
    public class CreateFacilityConfigurationCommand : ICreateFacilityConfigurationCommand
    {
        private readonly ILogger<GetFacilityConfigurationQuery> _logger;
        private readonly INotificationConfigurationRepository _datastore;
        private readonly INotificationConfigurationFactory _notificationConfigurationFactory;
        private readonly IAuditEventFactory _auditEventFactory;
        private readonly ICreateAuditEventCommand _createAuditEventCommand;

        public CreateFacilityConfigurationCommand(ILogger<GetFacilityConfigurationQuery> logger, IAuditEventFactory auditEventFactory, ICreateAuditEventCommand createAuditEventCommand, INotificationConfigurationRepository datastore, INotificationConfigurationFactory notificationConfigurationFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditEventFactory = auditEventFactory ?? throw new ArgumentNullException(nameof(auditEventFactory));
            _createAuditEventCommand = createAuditEventCommand ?? throw new ArgumentNullException(nameof(createAuditEventCommand));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
            _notificationConfigurationFactory = notificationConfigurationFactory ?? throw new ArgumentNullException(nameof(notificationConfigurationFactory));
        }

        public async Task<string> Execute(CreateFacilityConfigurationModel model)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Create Notification Configuration Command");         

            if (string.IsNullOrEmpty(model.FacilityId)) { throw new ArgumentNullException(nameof(model.FacilityId)); }

            try
            {
                using (ServiceActivitySource.Instance.StartActivity("Create the notification configuration"))
                {
                    NotificationConfig entity = _notificationConfigurationFactory.NotificationConfigEntityCreate(model.FacilityId, model.EmailAddresses, model.EnabledNotifications, model.Channels);
                    if (string.IsNullOrEmpty(entity.Id)) { entity.Id = Guid.NewGuid().ToString(); } //create new GUID for notification configuration
                                                                                                    //entity.CreatedBy =

                    _ = await _datastore.AddAsync(entity);

                    //TODO: Get user info
                    //Create audit event
                    string notes = $"New notification configuration ({entity.Id}) created for '{entity.FacilityId}'.";
                    AuditEventMessage auditEventMessage = _auditEventFactory.CreateAuditEvent(null, null, "SystemUser", AuditEventType.Create, typeof(NotificationConfig).Name, notes);
                    _ = Task.Run(() => _createAuditEventCommand.Execute(entity.FacilityId, auditEventMessage));                

                    //add id to current activity
                    var currentActivity = Activity.Current;
                    currentActivity?.AddTag("notification id", entity.Id);
                    currentActivity?.AddTag("facility id", entity.FacilityId);

                    //Log creation of new notification configuration                        
                    _logger.LogInformation(new EventId(NotificationLoggingIds.GenerateItems, "Notification Service - Create notification configuration"), "New notification configuration {id}' created for '{facilityId}'.", entity.Id, entity.FacilityId);
                    return entity.Id;
                }                           
            }
            catch (Exception ex)
            {
                ex.Data.Add("facility id", model.FacilityId);
                _logger.LogError(new EventId(NotificationLoggingIds.GenerateItems, "Notification Service - Create notification configuration"), ex, "Failed to create notification configuration for facility '{facilityId}'.", model.FacilityId);
                var currentActivity = Activity.Current;
                currentActivity?.SetStatus(ActivityStatusCode.Error, $"Failed to create notification configuration for facility {model.FacilityId}.");
                throw;
            }

        }
    }
}
