using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Audit.Infrastructure;
using LantanaGroup.Link.Audit.Infrastructure.Telemetry;
using LantanaGroup.Link.Audit.Settings;
using System.Diagnostics;
using static LantanaGroup.Link.Audit.Settings.AuditConstants;

namespace LantanaGroup.Link.Audit.Application.Commands
{
    public class CreateAuditEventCommand : ICreateAuditEventCommand
    {
        private readonly ILogger<CreateAuditEventCommand> _logger;
        private readonly IAuditRepository _datastore;
        private readonly IAuditFactory _factory;
        private readonly AuditServiceMetrics _metrics;

        public CreateAuditEventCommand(ILogger<CreateAuditEventCommand> logger, IAuditRepository datastore, IAuditFactory factory, AuditServiceMetrics metrics, IAuditHelper auditHelper) {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));        
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }
        
        /// <summary>
        /// A command to create a new audit event
        /// </summary>
        /// <param name="model">A model that represents to optoinal fields that can be used when creating an audit event (facilityId, serviceName, correlationId, eventDate, userId, user, action, resource, propertyChanges, and notes).</param>
        /// <returns>The id of the new autid event created</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ApplicationException"></exception>
        public async Task<string> Execute(CreateAuditEventModel model)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Create Audit Event Command");

            if (string.IsNullOrWhiteSpace(model.ServiceName))
            {
                throw new ArgumentException((string?)AuditConstants.AuditErrorMessages.NullOrWhiteSpaceServiceName);
            }

            model.EventDate ??= DateTime.Now;

            AuditEntity auditLog = _factory.Create(model.FacilityId, model.ServiceName, model.CorrelationId, model.EventDate, model.UserId, model.User, model.Action, model.Resource, model.PropertyChanges, model.Notes);
            if (String.IsNullOrEmpty(auditLog.Id)) { auditLog.Id = Guid.NewGuid().ToString(); } //create new GUID for audit event
           
            try
            {
                await _datastore.AddAsync(auditLog);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(new EventId(AuditLoggingIds.GenerateItems, "Audit Service - Create event"), ex, "New audit event creation failed for '{auditLogAction}' of resource '{auditLogResource}' in the '{auditLogServiceName}' service.", auditLog.Action, auditLog.Resource, auditLog.ServiceName);
                throw;
            }

            //Log creation of new audit event                        
            _logger.LogInformation(new EventId(AuditLoggingIds.GenerateItems, "Audit Service - Create event"), "New audit event ({auditLogId}) created for '{auditLogAction}' of resource '{auditLogResource}' in the '{auditLogServiceName}' service.", auditLog.Id, auditLog.Action, auditLog.Resource, auditLog.ServiceName);
            _metrics.AuditableEventCounter.Add(1, 
                new KeyValuePair<string, object?>("service", auditLog.ServiceName),
                new KeyValuePair<string, object?>("facility", auditLog.FacilityId),
                new KeyValuePair<string, object?>("action", auditLog.Action),
                new KeyValuePair<string, object?>("resource", auditLog.Resource)                
            );
            return auditLog.Id;

            
         
        }
    }
}
