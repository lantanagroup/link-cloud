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
       

        public async Task<bool> Execute(NotificationConfigId id, CancellationToken cancellationToken)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Delete Notification Configuration Command");

            try 
            {                
                var config = await _datastore.GetAsync(id, true, cancellationToken);

                using (ServiceActivitySource.Instance.StartActivity("Check if notification configuration exists"))
                {                    
                    if (config is null)
                    {
                        var message = $"No notification configuration with an id of {id.Value} found.";
                        _logger.LogNotificationConfigurationDeleteWarning(message);                        
                        return false;
                    }
                }

                using (ServiceActivitySource.Instance.StartActivity("Delete the facility configuration"))
                {                   

                    bool result = await _datastore.DeleteAsync(config.Id, cancellationToken);                    

                    if (result)
                    {
                        var message = $"Notificaiton configuration {config.Id.Value} was deleted.";
                        _logger.LogNotificationConfigurationDeletion(config.Id.Value.ToString(), message);

                        //TODO: Get user info
                        //Create audit event
                        string notes = $"Notification configuration ({config.Id.Value}) for facility {config.FacilityId} was deleted by TODO-USER.";
                        AuditEventMessage auditEventMessage = _auditEventFactory.CreateAuditEvent(null, null, "SystemUser", AuditEventType.Delete, typeof(NotificationEntity).Name, notes);
                        _ = Task.Run(() => _createAuditEventCommand.Execute(config.FacilityId, auditEventMessage));                  
                    }
                    else
                    {
                        _logger.LogNotificationConfigurationDeletion(config.Id.Value.ToString(), $"Notificaiton configuration {config.Id.Value} was not deleted.");
                    }

                    return result;
                }                
            }
            catch (NullReferenceException)
            {                
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
            
        }
    }
}
