using Confluent.Kafka;
using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Managers;
using LantanaGroup.Link.Audit.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text;

namespace LantanaGroup.Link.Audit.Application.Services
{
    public class AuditEventProcessor : IAuditEventProcessor
    {
        private readonly ILogger<AuditEventProcessor> _logger;
        private readonly IAuditManager _auditManager;

        public AuditEventProcessor(ILogger<AuditEventProcessor> logger, IAuditManager auditManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditManager = auditManager ?? throw new ArgumentNullException(nameof(auditManager));
        }

        public async Task<bool> ProcessAuditEvent(ConsumeResult<string, AuditEventMessage>? result, CancellationToken cancellationToken)
        {
            if (result is null)
            {
                throw new DeadLetterException("Invalid Auditable Event: The message received was null");
            }

            if (result.Message.Value is null)
            {
                throw new DeadLetterException("Invalid Auditable Event: The message value received was null");
            }

            AuditEventMessage messageValue = result.Message.Value;

            if (result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
            {
                messageValue.CorrelationId = Encoding.UTF8.GetString(headerValue);
            }

            //create audit event                                   
            var auditEventModel = AuditModel.FromMessage(messageValue);
            _logger.LogAuditableEventConsumption(result.Message.Key, messageValue.ServiceName ?? string.Empty, auditEventModel);
                        
            try
            {
                _ = await _auditManager.CreateAuditLog(auditEventModel, cancellationToken);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                throw new TransientException($"Unable to create audit log entry: {ex.Message}", ex);
            }

            return true;
        }
    }
}
