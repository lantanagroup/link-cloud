using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Notification.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models;
using System.Diagnostics;

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
                        var message = $"No notification configuration with an id of {id} found.";
                        _logger.LogNotificationConfigurationDeleteWarning(message);                        
                        return false;
                    }
                }

                using (ServiceActivitySource.Instance.StartActivity("Get existing configuration and delete it"))
                {
                    NotificationConfig config = await _datastore.GetAsync(id);

                    bool result = await _datastore.DeleteAsync(id);                    

                    if (result)
                    {
                        var message = $"Notificaiton configuration {id} was deleted.";
                        _logger.LogNotificationConfigurationDeletion(id, message);

                        //TODO: Get user info
                        //Create audit event
                        string notes = $"Notification configuration ({id}) for facility {config.FacilityId} was deleted by TODO-USER.";
                        AuditEventMessage auditEventMessage = _auditEventFactory.CreateAuditEvent(null, null, "SystemUser", AuditEventType.Delete, typeof(NotificationEntity).Name, notes);
                        _ = Task.Run(() => _createAuditEventCommand.Execute(config.FacilityId, auditEventMessage));                  
                    }
                    else
                    {
                        _logger.LogNotificationConfigurationDeletion(id, $"Notificaiton configuration {id} was not deleted.");
                    }

                    return result;
                }                
            }
            catch (NullReferenceException ex)
            {                
                Activity.Current?.SetStatus(ActivityStatusCode.Error, $"Failed to delete notificaiton configuration {id}");
                throw;
            }
            
        }
    }
}
