using Confluent.Kafka;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using System.Diagnostics;
using System.Text.Json;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{
    public class CreateReportScheduled : ICreateReportScheduled
    {
        private readonly ILogger<CreateReportScheduled> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public CreateReportScheduled(ILogger<CreateReportScheduled> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));            
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        public async Task<string> Execute(ReportScheduled model, string? userId = null)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Producing Report Scheduled Event");
            using var scope = _scopeFactory.CreateScope();
            var _kafkaProducerFactory = scope.ServiceProvider.GetRequiredService<IKafkaProducerFactory<string, object>>();

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
                var producerConfig = new ProducerConfig();

                using (var producer = _kafkaProducerFactory.CreateProducer(producerConfig, useOpenTelemetry: true))
                {
                    try
                    {
                        var headers = new Headers();
                        headers.Add("X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(correlationId));

                        ReportScheduledKey Key = new ReportScheduledKey()
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

                        await producer.ProduceAsync(nameof(KafkaTopic.ReportScheduled), message);

                        _logger.LogInformation($"New Report Scheduled ({correlationId}) created.");
                        return correlationId;

                    }
                    catch (Exception ex)
                    {
                        Activity.Current?.SetStatus(ActivityStatusCode.Error);
                        _logger.LogKafkaProducerException(correlationId, ex.Message);
                        throw;
                    }

                }
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                _logger.LogKafkaProducerException(nameof(KafkaTopic.PatientEvent), ex.Message);
                throw;
            }


        }
    }
}
