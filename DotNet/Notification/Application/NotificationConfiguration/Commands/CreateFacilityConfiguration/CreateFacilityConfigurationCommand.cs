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
    public class CreateFacilityConfigurationCommand : ICreateFacilityConfigurationCommand
    {
        private readonly ILogger<GetFacilityConfigurationQuery> _logger;
        private readonly INotificationConfigurationRepository _datastore;
        private readonly INotificationConfigurationFactory _notificationConfigurationFactory;
        private readonly ICreateAuditEventCommand _createAuditEventCommand;
        private readonly IAuditEventFactory _auditEventFactory;

        public CreateFacilityConfigurationCommand(ILogger<GetFacilityConfigurationQuery> logger, INotificationConfigurationRepository datastore, INotificationConfigurationFactory notificationConfigurationFactory, ICreateAuditEventCommand createAuditEventCommand, IAuditEventFactory auditEventFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
            _notificationConfigurationFactory = notificationConfigurationFactory ?? throw new ArgumentNullException(nameof(notificationConfigurationFactory));
            _createAuditEventCommand = createAuditEventCommand ?? throw new ArgumentNullException(nameof(createAuditEventCommand));
            _auditEventFactory = auditEventFactory ?? throw new ArgumentNullException(nameof(auditEventFactory));
        }

        public async Task<NotificationConfigurationModel> Execute(CreateFacilityConfigurationModel model, CancellationToken cancellationToken)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Create Notification Configuration Command");         

            if (string.IsNullOrEmpty(model.FacilityId)) { throw new ArgumentNullException(nameof(model.FacilityId)); }

            try
            {
                using (ServiceActivitySource.Instance.StartActivity("Create the notification configuration"))
                {
                    NotificationConfig entity = _notificationConfigurationFactory.NotificationConfigEntityCreate(model.FacilityId, model.EmailAddresses, model.EnabledNotifications, model.Channels);
                    
                    bool outcome = await _datastore.AddAsync(entity, cancellationToken);              

                    if(!outcome)
                    {
                        Activity.Current?.SetStatus(ActivityStatusCode.Error);
                        return null;
                    }                   

                    //add id to current activity
                    var currentActivity = Activity.Current;
                    currentActivity?.AddTag("notification.id", entity.Id.Value);
                    currentActivity?.AddTag("facility.id", entity.FacilityId);

                    //TODO: Get user info
                    //Create audit event
                    string notes = $"New notification configuration ({entity.Id.Value}) created for '{entity.FacilityId}'.";
                    AuditEventMessage auditEventMessage = _auditEventFactory.CreateAuditEvent(null, null, "SystemUser", AuditEventType.Create, typeof(NotificationConfig).Name, notes);
                    _ = Task.Run(() => _createAuditEventCommand.Execute(entity.FacilityId, auditEventMessage));

                    //Log creation of new notification configuration                       
                    _logger.LogNotificationConfigurationCreation(entity.Id.Value.ToString(), model.FacilityId, model);

                    var config = _notificationConfigurationFactory.NotificationConfigurationModelCreate(entity.Id, entity.FacilityId, entity.EmailAddresses, entity.EnabledNotifications, entity.Channels);
                    return config;
                }                           
            }
            catch (Exception)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }

        }
    }
}
