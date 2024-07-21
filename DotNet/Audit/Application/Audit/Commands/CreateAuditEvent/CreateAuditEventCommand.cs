using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Audit.Infrastructure;
using LantanaGroup.Link.Audit.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using OpenTelemetry.Trace;
using System.Diagnostics;
using static LantanaGroup.Link.Audit.Settings.AuditConstants;

namespace LantanaGroup.Link.Audit.Application.Commands
{
    public class CreateAuditEventCommand : ICreateAuditEventCommand
    {
        private readonly ILogger<CreateAuditEventCommand> _logger;
        private readonly IAuditRepository _datastore;
        private readonly IAuditFactory _factory;
        private readonly IAuditServiceMetrics _metrics;

        public CreateAuditEventCommand(ILogger<CreateAuditEventCommand> logger, IAuditRepository datastore, IAuditFactory factory, IAuditServiceMetrics metrics) {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));        
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        /// <summary>
        /// A command to create a new audit event
        /// </summary>
        /// <param name="model">A model that represents to optional fields that can be used when creating an audit event (facilityId, serviceName, correlationId, eventDate, userId, user, action, resource, propertyChanges, and notes).</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The id of the new autid event created</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ApplicationException"></exception>
        public async Task<AuditLog> Execute(CreateAuditEventModel model, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance
                .StartActivityWithTags(DiagnosticNames.CreateAuditEvent,
                [
                    new KeyValuePair<string, object?>(DiagnosticNames.Service, model.ServiceName),
                    new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, model.FacilityId),
                    new KeyValuePair<string, object?>(DiagnosticNames.AuditLogAction, model.Action),
                    new KeyValuePair<string, object?>(DiagnosticNames.Resource, model.Resource)
                ]);                                   

            model.EventDate ??= DateTime.Now;

            AuditLog auditLog = _factory.Create(model.FacilityId, model.ServiceName, model.CorrelationId, model.EventDate, model.UserId, model.User, model.Action, model.Resource, model.PropertyChanges, model.Notes);
                       
            try
            {
                await _datastore.AddAsync(auditLog, cancellationToken);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogDebug(new EventId(AuditLoggingIds.GenerateItems, "Audit Service - Create event"), ex, "New audit event creation failed for '{auditLogAction}' of resource '{auditLogResource}' in the '{auditLogServiceName}' service.", auditLog.Action, auditLog.Resource, auditLog.ServiceName);
                throw;
            }

            //Log creation of new audit event                                 
            _logger.LogAuditEventCreation(auditLog);
            _metrics.IncrementAuditableEventCounter([
                new KeyValuePair<string, object?>(DiagnosticNames.Service, auditLog.ServiceName),
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, auditLog.FacilityId),
                new KeyValuePair<string, object?>(DiagnosticNames.AuditLogAction, auditLog.Action),
                new KeyValuePair<string, object?>(DiagnosticNames.Resource, auditLog.Resource)
            ]);                
          
            return auditLog;           
         
        }
    }
}
