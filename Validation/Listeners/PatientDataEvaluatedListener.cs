using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Validation.Models;
using LantanaGroup.Link.Shared.Application.Models;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Validation.Entities;
using System.Text.Json;
using Hl7.Fhir.Serialization;

namespace LantanaGroup.Link.Validation.Listeners
{
    public class PatientDataEvaluatedListener : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly IKafkaConsumerFactory<PatientDataEvaluatedKey, PatientDataEvaluatedMessage> _consumerFactory;
        private readonly IKafkaProducerFactory<string, MeasureReportValidationMessage> _producerFactory;
        private readonly IMongoDbRepository<ValidationEntity> validationRepository;
        private bool _cancelled;

        public PatientDataEvaluatedListener(
            ILogger<PatientDataEvaluatedListener> logger,
            IKafkaConsumerFactory<PatientDataEvaluatedKey, PatientDataEvaluatedMessage> kafkaConsumerFactory,
            IKafkaProducerFactory<string, MeasureReportValidationMessage> kafkaProducerFactory,
            IMongoDbRepository<ValidationEntity> repository) {
            this._logger = logger;
            this._consumerFactory = kafkaConsumerFactory;
            this._producerFactory = kafkaProducerFactory;
            this.validationRepository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        ~PatientDataEvaluatedListener()
        {

        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            this._cancelled = true;
            await Task.CompletedTask;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken stoppingToken)
        {
            ConsumerConfig config = new ConsumerConfig
            {
                GroupId = "Validation-PatientDataEvaluated",
                EnableAutoCommit = false
            };

            using (var kafkaConsumer = _consumerFactory.CreateConsumer(config))
            {
                kafkaConsumer.Subscribe(new string[] { KafkaTopic.MeasureEvaluated.ToString() });
                _logger.LogTrace($"Subscribed to topic {KafkaTopic.MeasureEvaluated}");

                while (!this._cancelled)
                {

                    /*
                     bundleAsString = JsonSerializer.Serialize(reportDefinition.Bundle);
                    var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
                    measureDefinition.bundle = JsonSerializer.Deserialize<Bundle>(bundleAsString, options);
                     */
                    var measureReport = new MeasureReport();
                    ConsumeResult<PatientDataEvaluatedKey, PatientDataEvaluatedMessage> message;
                    try
                    {
                        message = kafkaConsumer.Consume();
                        var mr = JsonSerializer.Serialize(message.Message.Value.Result);
                        var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
                        measureReport = JsonSerializer.Deserialize<MeasureReport>(mr, options);
                    }
                    catch (Exception ex)
                    {
                        this._logger.LogError(ex.Message, ex);
                        continue;
                    }

                    if (message == null)
                        continue;
                    _logger.LogTrace($"Consuming validation message");

                    //TODO: Validate measure reports here
                    //Replace this true in the statement below with actual validation process.  This will probably be either an http response or some kind of response parser of the
                    //FHIR Validation jar
                    bool valid = true;

                    //Store result of validation on data for patient with id of patientId from tenant with id of tenantId
                    var report = new ValidationEntity
                    {
                        IsValid = valid,
                        PatientId = message.Message.Value.PatientId,
                        TenantId = message.Message.Value.TenantId
                    };
                    validationRepository.Add(report);

                    if (valid)
                    {
                        using var kafkaProducer = _producerFactory.CreateProducer(new ProducerConfig());

                        await kafkaProducer.ProduceAsync("Validation", new Message<string, MeasureReportValidationMessage>
                        {
                            Key = measureReport != null ? measureReport.Id: message.Message.Value.PatientId,
                            Value = new MeasureReportValidationMessage()
                            {
                                validationMessage = "Validation results here",
                                validationResult = true
                            }
                        });
                    }
                }
            }
            
            
        }
    }
}
