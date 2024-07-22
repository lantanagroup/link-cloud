using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Audit.Infrastructure;
using LantanaGroup.Link.Audit.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Extensions.Telemetry;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using OpenTelemetry.Trace;
using System.Diagnostics;
using static LantanaGroup.Link.Audit.Settings.AuditConstants;

namespace LantanaGroup.Link.Audit.Domain.Managers
{
    public interface IAuditManager
    {
        Task<AuditLog> CreateAuditLog(AuditModel model, CancellationToken cancellationToken = default);
    }

    public class AuditManager : IAuditManager
    {
        private readonly ILogger<AuditManager> _logger;
        private readonly IAuditRepository _datastore;
        private readonly IAuditServiceMetrics _metrics;

        public AuditManager(ILogger<AuditManager> logger, IAuditRepository datastore, IAuditServiceMetrics metrics)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        public async Task<AuditLog> CreateAuditLog(AuditModel model, CancellationToken cancellationToken = default)
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
            AuditLog auditLog = AuditModel.ToDomain(model);

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
