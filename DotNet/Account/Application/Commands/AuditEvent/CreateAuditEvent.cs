using Confluent.Kafka;
using LantanaGroup.Link.Account.Infrastructure;
using LantanaGroup.Link.Account.Infrastructure.Logging;
using LantanaGroup.Link.Account.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text;

namespace LantanaGroup.Link.Account.Application.Commands.AuditEvent
{
    public class CreateAuditEvent : ICreateAuditEvent
    {
        private readonly ILogger<CreateAuditEvent> _logger;
        private readonly IProducer<string, object> _producer;

        public CreateAuditEvent(ILogger<CreateAuditEvent> logger, IProducer<string, object> producer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        public async Task<bool> Execute(AuditEventMessage model, CancellationToken cancellationToken = default)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("CreateAuditEvent:Execute");

            try
            {
                model.ServiceName = AccountConstants.ServiceName;

                // send the Audit Event
                Headers headers = [];

                if(!string.IsNullOrEmpty(model.CorrelationId))
                {
                    headers.Add("X-Correlation-Id", Encoding.ASCII.GetBytes(model.CorrelationId));
                }

                await _producer.ProduceAsync(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, object>
                {
                    Key = model.FacilityId ?? string.Empty,
                    Value = model,
                    Headers = headers
                }, cancellationToken);

                _logger.LogAuditEventCreated(model);

                return true;
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogAuditEventCreationException(ex.Message, model);
                throw;
            }              
        }
    }
}
