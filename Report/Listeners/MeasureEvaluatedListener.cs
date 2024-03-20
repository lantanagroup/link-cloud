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
using System.Transactions;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.Report.Application.Error.Interfaces;
using LantanaGroup.Link.Report.Application.Error.Exceptions;

namespace LantanaGroup.Link.Report.Listeners
{
    public class MeasureEvaluatedListener : BackgroundService
    {

        private readonly ILogger<MeasureEvaluatedListener> _logger;
        private readonly IKafkaConsumerFactory<MeasureEvaluatedKey, MeasureEvaluatedValue> _kafkaConsumerFactory;
        private readonly IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue> _kafkaProducerFactory;
        private readonly IMediator _mediator;

        private readonly IReportTransientExceptionHandler<MeasureEvaluatedKey, MeasureEvaluatedValue> _reportTransientExceptionHandler;
        private readonly IReportExceptionHandler<MeasureEvaluatedKey, MeasureEvaluatedValue> _reportExceptionHandler;

        public MeasureEvaluatedListener(ILogger<MeasureEvaluatedListener> logger, IKafkaConsumerFactory<MeasureEvaluatedKey, MeasureEvaluatedValue> kafkaConsumerFactory,
            IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue> kafkaProducerFactory, IMediator mediator,
            IReportTransientExceptionHandler<MeasureEvaluatedKey, MeasureEvaluatedValue> reportTransientExceptionHandler,
            IReportExceptionHandler<MeasureEvaluatedKey, MeasureEvaluatedValue> reportExceptionHandler)
        {

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentException(nameof(kafkaProducerFactory));
            _mediator = mediator ?? throw new ArgumentException(nameof(mediator));

            _reportTransientExceptionHandler = reportTransientExceptionHandler ?? throw new ArgumentException(nameof(reportTransientExceptionHandler));
            _reportExceptionHandler = reportExceptionHandler ?? throw new ArgumentException(nameof(reportExceptionHandler));
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

            using var consumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.MeasureEvaluated));
                _logger.LogInformation($"Started measure evaluated consumer for topic '{nameof(KafkaTopic.MeasureEvaluated)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var consumeResult = new ConsumeResult<MeasureEvaluatedKey, MeasureEvaluatedValue>();
                    try
                    {
                        consumeResult = consumer.Consume(cancellationToken);

                        if (consumeResult != null)
                        {
                            MeasureEvaluatedKey key = consumeResult.Message.Key;
                            MeasureEvaluatedValue value = consumeResult.Message.Value;

                            if (!consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                            {
                                throw new TerminatingException($"MeasureEvaluatedListener: Received message without correlation ID: {consumeResult.Topic}");
                            }

                            if (string.IsNullOrWhiteSpace(key.FacilityId) ||
                                string.IsNullOrWhiteSpace(key.ReportType) ||
                                key.StartDate == DateTime.MinValue ||
                                key.EndDate == DateTime.MinValue)
                            {
                                throw new TerminatingException(
                                    "MeasureEvaluatedListener: One or more required MeasureEvaluatedKey properties are null or empty.");
                            }

                            // find existing report scheduled for this facility, report type, and date range
                            var schedule = await _mediator.Send(new FindMeasureReportScheduleForReportTypeQuery { FacilityId = key.FacilityId, ReportStartDate = key.StartDate, ReportEndDate = key.EndDate, ReportType = key.ReportType }, cancellationToken)
                                                    ?? throw new TransactionException($"No report schedule found for Facility {key.FacilityId} and reporting period of {key.StartDate} - {key.EndDate} for {key.ReportType}");
                            var measureReport = new MeasureReport();

                            try
                            {
                                measureReport = JsonSerializer.Deserialize<MeasureReport>(value.Result,
                                    new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector));
                            }
                            catch (Exception ex)
                            {
                                throw new TerminatingException(
                                    "MeasureEvaluatedListener: Unable to deserialize MeasureEvaluatedValue.Result");
                            }

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

                            #region Patients To Query & Submision Report Handling
                            if (schedule.PatientsToQueryDataRequested.GetValueOrDefault())
                            {
                                if (schedule.PatientsToQuery?.Contains(value.PatientId) ?? false)
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

                            consumer.Commit(consumeResult);
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        consumer.Commit(consumeResult);
                        _reportExceptionHandler.HandleException(consumeResult, new TerminatingException("MeasureEvaluatedListener: " + ex.Message, ex.InnerException));
                    }
                    catch (TerminatingException ex)
                    {
                        consumer.Commit(consumeResult);
                        _reportExceptionHandler.HandleException(consumeResult, ex);
                    }
                    catch (TransientException ex)
                    {
                        _reportTransientExceptionHandler.HandleException(consumeResult, ex);
                        consumer.Commit(consumeResult);
                    }
                    catch (Exception ex)
                    {
                        consumer.Commit(consumeResult);
                        _reportExceptionHandler.HandleException(consumeResult, new TerminatingException("MeasureEvaluatedListener: " + ex.Message, ex.InnerException));
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError($"Operation Canceled: {oce.Message}", oce);
                consumer.Close();
                consumer.Dispose();
            }

        }

    }
}
