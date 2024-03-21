using Confluent.Kafka;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using System.Diagnostics;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration
{
    public class CreateDataAcquisitionRequested : ICreateDataAcquisitionRequested
    {
        private readonly ILogger<CreateDataAcquisitionRequested> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public CreateDataAcquisitionRequested(ILogger<CreateDataAcquisitionRequested> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        public async Task<string> Execute(DataAcquisitionRequested model, string? userId = null)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Producing Report Scheduled Event");
            using var scope = _scopeFactory.CreateScope();
            var _kafkaProducerFactory = scope.ServiceProvider.GetRequiredService<IKafkaProducerFactory<string, object>>();

            string correlationId = Guid.NewGuid().ToString();

            try
            {
                var producerConfig = new ProducerConfig();

                using (var producer = _kafkaProducerFactory.CreateProducer(producerConfig))
                {
                    try
                    {
                        var headers = new Headers();
                        headers.Add("X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(correlationId));

                        var message = new Message<string, object>
                        {
                            Key = model.Key,
                            Value = new DataAcquisitionRequestedMessage { PatientId = model.PatientId, reports = model.Reports },
                            Headers = headers
                        };

                        await producer.ProduceAsync(nameof(KafkaTopic.DataAcquisitionRequested), message);

                        _logger.LogKafkaProducerDataAcquisitionRequested(correlationId);
                        return correlationId;

                    }
                    catch (Exception ex)
                    {
                        Activity.Current?.SetStatus(ActivityStatusCode.Error);
                        _logger.LogKafkaProducerException(nameof(KafkaTopic.PatientEvent), ex.Message);
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
