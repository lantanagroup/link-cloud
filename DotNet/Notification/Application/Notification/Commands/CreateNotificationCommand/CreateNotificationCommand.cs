using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.NotificationConfiguration.Queries;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Notification.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using System.Diagnostics;

namespace LantanaGroup.Link.Notification.Application.Notification.Commands
{
    public class CreateNotificationCommand : ICreateNotificationCommand
    {
        private readonly ILogger<CreateNotificationCommand> _logger;
        private readonly IAuditEventFactory _auditEventFactory;
        private readonly ICreateAuditEventCommand _createAuditEventCommand;
        private readonly INotificationRepository _datastore;
        private readonly INotificationFactory _notificationFactory;
        private readonly IGetFacilityConfigurationQuery _getFacilityConfigurationQuery;
        private readonly INotificationServiceMetrics _metrics;

        public CreateNotificationCommand(ILogger<CreateNotificationCommand> logger, IAuditEventFactory auditEventFactory, ICreateAuditEventCommand createAuditEventCommand, INotificationRepository datastore, INotificationFactory notificationFactory, IGetFacilityConfigurationQuery getFacilityConfigurationQuery, INotificationServiceMetrics metrics)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));    
            _auditEventFactory = auditEventFactory ?? throw new ArgumentNullException(nameof(auditEventFactory));
            _createAuditEventCommand = createAuditEventCommand ?? throw new ArgumentNullException(nameof(createAuditEventCommand));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
            _notificationFactory = notificationFactory ?? throw new ArgumentNullException(nameof(notificationFactory));
            _getFacilityConfigurationQuery = getFacilityConfigurationQuery ?? throw new ArgumentNullException(nameof(getFacilityConfigurationQuery));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        public async Task<string> Execute(CreateNotificationModel model, CancellationToken cancellationToken)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Create Notification Command");

            if (model == null) throw new ArgumentNullException(nameof(model));

            if (string.IsNullOrEmpty(model.NotificationType)) { throw new ArgumentNullException(nameof(model.NotificationType)); }           

            if (string.IsNullOrEmpty(model.Subject)) { throw new ArgumentNullException(nameof(model.Subject)); }

            if (string.IsNullOrEmpty(model.Body)) { throw new ArgumentNullException(nameof(model.Body)); }

           //if a notification is facility generated
            if (!string.IsNullOrEmpty(model.FacilityId))
            {
                using (ServiceActivitySource.Instance.StartActivity("Get facility configuration for notifications"))
                {
                    //get facility configuration to determine recipients
                    NotificationConfigurationModel config = await _getFacilityConfigurationQuery.Execute(model.FacilityId, cancellationToken);

                    if (config is null || config.EmailAddresses is null || config.EmailAddresses.Count == 0)
                    {
                        throw new ApplicationException($"Facility '{model.FacilityId}' does not have any recipients configured.");
                    }


                    //add configured email addresses                
                    if (model.Recipients is null) { model.Recipients = new List<string>(); }

                    using (ServiceActivitySource.Instance.StartActivity("Add recipients from facility configuration"))
                    {
                        //if recipients were sent with the notificationr request, combine with facility emails and remove duplicates                           
                        List<string> emails = new List<string>();
                        emails.AddRange(model.Recipients);
                        emails.AddRange(config.EmailAddresses);

                        //remove duplicate emails
                        model.Recipients.Clear();
                        model.Recipients.AddRange(emails.Distinct().ToList());
                    }                    
                }               
            }
            else //if not a facility notification
            {
                //then check to make sure recipients have been included
                if (model.Recipients is null || model.Recipients.Count == 0) { throw new ArgumentNullException(nameof(model.Recipients)); }
            }

            //TODO - In future version, check enabled notifications to determine if it should be created
            try
            {
                using (ServiceActivitySource.Instance.StartActivity("Create the notification"))
                {
                    NotificationEntity entity = _notificationFactory.NotificationEntityCreate(model.NotificationType, model.FacilityId, model.CorrelationId, model.Subject, model.Body, model.Recipients, model.Bcc);
                    _ = await _datastore.AddAsync(entity);       

                    //TODO: Get user info
                    //Create audit event
                    string notes = $"New notification ({entity.Id}) of type '{entity.NotificationType}' created for '{entity.FacilityId}'.";
                    AuditEventMessage auditEventMessage = _auditEventFactory.CreateAuditEvent(model.CorrelationId, null, "SystemUser", AuditEventType.Create, typeof(NotificationEntity).Name, notes);
                    _ = Task.Run(() => _createAuditEventCommand.Execute(entity.FacilityId, auditEventMessage));                    

                    //add id to current activity
                    var currentActivity = Activity.Current;
                    currentActivity?.AddTag(DiagnosticNames.NotificationId, entity.Id);
                    if (entity.FacilityId != null)
                    {
                        currentActivity?.AddTag(DiagnosticNames.FacilityId, entity.FacilityId);
                    }

                    //update notification creation metric counter                    
                    _metrics.IncrementNotificationCreatedCounter([ 
                        new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, entity.FacilityId), 
                        new KeyValuePair<string, object?>(DiagnosticNames.NotificationType, entity.NotificationType)
                    ]);

                    //Log creation of new notification configuration
                    _logger.LogNotificationCreation(entity.Id.Value.ToString(), model);                    
                    return entity.Id.Value.ToString();
                }              
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogNotificationCreationException(model, ex.Message);
                throw;
            }
        }
    }
}
