using System.Text;
using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Commands;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries;
using LantanaGroup.Link.Report.Application.MeasureReportSubmissionEntry.Commands;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using MediatR;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Listeners
{
    public class MeasureEvaluatedListener : BackgroundService
    {

        private readonly ILogger<MeasureEvaluatedListener> _logger;
        private readonly IKafkaConsumerFactory<MeasureEvaluatedKey, MeasureEvaluatedValue> _kafkaConsumerFactory;
        private readonly IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue> _kafkaProducerFactory;
        private readonly IMediator _mediator;


        public MeasureEvaluatedListener(ILogger<MeasureEvaluatedListener> logger, IKafkaConsumerFactory<MeasureEvaluatedKey, MeasureEvaluatedValue> kafkaConsumerFactory, 
            IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue> kafkaProducerFactory, IMediator mediator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentException(nameof(kafkaProducerFactory));
            _mediator = mediator ?? throw new ArgumentException(nameof(mediator));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }


        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var consumerConfig = new ConsumerConfig()
            {
                GroupId = "MeasureEvaluatedEvent",
                EnableAutoCommit = false
            };

            ProducerConfig producerConfig = new ProducerConfig()
            {
                ClientId = "Report_SubmissionReportScheduled"
            };

            using (var _measureEvaluatedConsumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig))
            {
                try
                {
                    _measureEvaluatedConsumer.Subscribe(nameof(KafkaTopic.MeasureEvaluated));
                    _logger.LogInformation($"Started measure evaluated consumer for topic '{nameof(KafkaTopic.MeasureEvaluated)}' at {DateTime.UtcNow}");

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            var consumeResult = _measureEvaluatedConsumer.Consume(cancellationToken);

                            if (consumeResult != null)
                            {
                                MeasureEvaluatedKey key = consumeResult.Message.Key;
                                MeasureEvaluatedValue value = consumeResult.Message.Value;

                                if (!consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                                {
                                    _logger.LogInformation($"Received message without correlation ID: {consumeResult.Topic}");
                                }

                                _logger.LogInformation($"Consumed Event for: Facility '{key.FacilityId}' has a report type of '{key.ReportType}' with a report period of {key.StartDate} to {key.EndDate}");

                                // find existing report scheduled for this facility, report type, and date range
                                var schedule = await _mediator.Send(new FindMeasureReportScheduleForReportTypeQuery { FacilityId = key.FacilityId, ReportStartDate = key.StartDate, ReportEndDate = key.EndDate, ReportType = key.ReportType }, cancellationToken) 
                                                        ?? throw new Exception($"No report schedule found for Facility {key.FacilityId} and reporting period of {key.StartDate} - {key.EndDate} for {key.ReportType}");

                                var measureReport = JsonSerializer.Deserialize<MeasureReport>(value.Result, new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector));

                                // ensure measure report has an ID to avoid inserting duplicates during bundling
                                if (string.IsNullOrEmpty(measureReport.Id))
                                {
                                    measureReport.Id = Guid.NewGuid().ToString();
                                }

                                // add this measure report to the measure report entry collection
                                MeasureReportSubmissionEntryModel entry = new MeasureReportSubmissionEntryModel 
                                {
                                    FacilityId = key.FacilityId,
                                    MeasureReportScheduleId = schedule.Id,
                                    PatientId = value.PatientId,
                                    MeasureReport = await new FhirJsonSerializer().SerializeToStringAsync(measureReport)
                                };

                                await _mediator.Send(new CreateMeasureReportSubmissionEntryCommand
                                    {
                                        MeasureReportSubmissionEntry = entry
                                    }, cancellationToken);

                                _logger.LogInformation($"MeasureReport added to MeasureReportSubmissionEntry { entry.Id }");

                                #region Patients To Query & Submision Report Handling
                                if (schedule.PatientsToQueryDataRequested.GetValueOrDefault())
                                {
                                    if(schedule.PatientsToQuery?.Contains(value.PatientId) ?? false)
                                    {
                                        schedule.PatientsToQuery.Remove(value.PatientId);

                                        await _mediator.Send(new UpdateMeasureReportScheduleCommand
                                        {
                                            ReportSchedule = schedule
                                        }, cancellationToken);
                                    }

                                    if (schedule.PatientsToQuery?.Count == 0)
                                    {
                                        using var prod = _kafkaProducerFactory.CreateProducer(producerConfig);
                                        prod.Produce(nameof(KafkaTopic.SubmitReport),
                                            new Message<SubmissionReportKey, SubmissionReportValue>
                                            {
                                                Key = new SubmissionReportKey()
                                                {
                                                    FacilityId = schedule.FacilityId,
                                                    ReportType = schedule.ReportType
                                                },
                                                Value = new SubmissionReportValue()
                                                {
                                                    MeasureReportScheduleId = schedule.Id
                                                },
                                                Headers = new Headers
                                                {
                                                    { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) }
                                                }
                                            });

                                        prod.Flush(cancellationToken);
                                    }
                                }
                                #endregion

                                _measureEvaluatedConsumer.Commit(consumeResult);
                            }
                        }
                        catch (ConsumeException e)
                        {
                            _logger.LogError($"Consumer error: {e.Error.Reason}");
                            _logger.LogError(e.InnerException?.Message);
                            if (e.Error.IsFatal)
                            {
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"An exception occurred in the Measure Evaluated Consumer service: {ex.Message}", ex);
                        }
                    }
                }
                catch (OperationCanceledException oce)
                {
                    _logger.LogError($"Operation Canceled: {oce.Message}", oce);
                    _measureEvaluatedConsumer.Close();
                    _measureEvaluatedConsumer.Dispose();
                }
            }

        }

    }
}
