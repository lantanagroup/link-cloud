using Confluent.Kafka;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Models;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text.Json;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{
    public class CreateReportScheduled : ICreateReportScheduled
    {
        private readonly ILogger<CreateReportScheduled> _logger;
        private readonly IProducer<string, object> _producer;

        public CreateReportScheduled(ILogger<CreateReportScheduled> logger, IProducer<string, object> producer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        public async Task<string> Execute(ReportScheduled model, string? userId = null)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Producing Report Scheduled Event");
            string correlationId = Guid.NewGuid().ToString();

            List<KeyValuePair<string, object>> parameters = [];
            if (model.StartDate is not null && model.EndDate is not null)
            {
                parameters.Add(new KeyValuePair<string, Object>("StartDate", model.StartDate));
                parameters.Add(new KeyValuePair<string, Object>("EndDate", model.EndDate));
            }
            else
            {
                throw new ArgumentNullException("Start and End date for report period cannot be null");
            }

            try
            {
                var headers = new Headers
                {
                    { "X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(correlationId) }
                };

                ReportScheduledKey Key = new()
                {
                    FacilityId = model.FacilityId,
                    ReportType = model.ReportType
                };

                var message = new Message<string, object>
                {
                    Key = JsonSerializer.Serialize(Key),
                    Headers = headers,
                    Value = new ReportScheduledMessage()
                    {
                        Parameters = parameters
                    },
                };

                await _producer.ProduceAsync(nameof(KafkaTopic.ReportScheduled), message);
                _logger.LogKafkaProducerReportScheduled(correlationId);

                return correlationId;

            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(ex);
                _logger.LogKafkaProducerException(correlationId, ex.Message);
                throw;
            }

        }
    }
}
