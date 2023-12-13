using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Shared.Application.Models;
using System.Diagnostics;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;

namespace LantanaGroup.Link.Notification.Application.NotificationConfiguration.Commands
{
    public class DeleteFacilityConfigurationCommand : IDeleteFacilityConfigurationCommand
    {
        private readonly ILogger<DeleteFacilityConfigurationCommand> _logger;
        private readonly INotificationConfigurationRepository _datastore;
        private readonly IAuditEventFactory _auditEventFactory;
        private readonly ICreateAuditEventCommand _createAuditEventCommand;

        public DeleteFacilityConfigurationCommand(ILogger<DeleteFacilityConfigurationCommand> logger, INotificationConfigurationRepository datastore, IAuditEventFactory auditEventFactory, ICreateAuditEventCommand createAuditEventCommand) 
        { 
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(_datastore));
            _auditEventFactory = auditEventFactory ?? throw new ArgumentNullException(nameof(auditEventFactory));
            _createAuditEventCommand = createAuditEventCommand ?? throw new ArgumentNullException(nameof(createAuditEventCommand));
        }
       

        public async Task<bool> Execute(string id)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Delete Notification Configuration Command");

            try 
            {
                if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

                using (ServiceActivitySource.Instance.StartActivity("Check if notification configuration exists"))
                {
                    if (!_datastore.Exists(id))
                    {
                        _logger.LogWarning(new EventId(NotificationLoggingIds.DeleteItem, "Notification Service - Delete notification configuration"), "No notification configuration with an id of {id} found.", id);
                        return false;
                    }
                }

                using (ServiceActivitySource.Instance.StartActivity("Get existing configuration and delete it"))
                {
                    NotificationConfig config = await _datastore.GetAsync(id);

                    bool result = await _datastore.DeleteAsync(id);                    

                    if (result)
                    {
                        _logger.LogInformation(new EventId(NotificationLoggingIds.DeleteItem, "Notification Service - Delete notification configuration"), "Notificaiton configuration {id} was deleted.", id);
                                          
                        //TODO: Get user info
                        //Create audit event
                        string notes = $"Notification configuration ({id}) for facility {config.FacilityId} was deleted by TODO-USER.";
                        AuditEventMessage auditEventMessage = _auditEventFactory.CreateAuditEvent(null, null, "SystemUser", AuditEventType.Delete, typeof(NotificationEntity).Name, notes);
                        _ = Task.Run(() => _createAuditEventCommand.Execute(config.FacilityId, auditEventMessage));                  
                    }
                    else
                    {
                        _logger.LogInformation(new EventId(NotificationLoggingIds.DeleteItem, "Notification Service - Delete notification configuration"), "Notificaiton configuration {id} was not deleted.", id);
                    }

                    return result;
                }                
            }
            catch (NullReferenceException ex)
            {
                _logger.LogError(new EventId(NotificationLoggingIds.DeleteItem, "Notification Service - Delete notification configuration"), ex, "Failed to delete notificaiton configuration {id}", id);
                var currentActivity = Activity.Current;
                currentActivity?.SetStatus(ActivityStatusCode.Error, $"Failed to delete notificaiton configuration {id}");
                throw;
            }
            
        }
    }
}
