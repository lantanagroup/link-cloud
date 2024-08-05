using Confluent.Kafka;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{
    public class CreateDataAcquisitionRequested : ICreateDataAcquisitionRequested
    {
        private readonly ILogger<CreateDataAcquisitionRequested> _logger;
        private readonly IProducer<string, object> _producer;

        public CreateDataAcquisitionRequested(ILogger<CreateDataAcquisitionRequested> logger, IProducer<string, object> producer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        public async Task<string> Execute(DataAcquisitionRequested model, string? userId = null)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Producing Data Acquisition Requested Event");
            string correlationId = Guid.NewGuid().ToString();

            try
            {
                var headers = new Headers
                {
                    { "X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(correlationId) }
                };

                var message = new Message<string, object>
                {
                    Key = model.Key,
                    Value = new DataAcquisitionRequestedMessage { PatientId = model.PatientId, QueryType = model.QueryType, ScheduledReports = model.ScheduledReports },
                    Headers = headers
                };

                await _producer.ProduceAsync(nameof(KafkaTopic.DataAcquisitionRequested), message);
                _logger.LogKafkaProducerDataAcquisitionRequested(correlationId);

                return correlationId;

            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogKafkaProducerException(nameof(KafkaTopic.PatientEvent), ex.Message);
                throw;
            }

        }
    }
}
