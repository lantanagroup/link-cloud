using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Commands;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using MediatR;
using System.Text;

namespace LantanaGroup.Link.Report.Listeners
{
    public class PatientIdsAcquiredListener : BackgroundService
    {
        private readonly ILogger<PatientIdsAcquiredListener> _logger;
        private readonly IKafkaConsumerFactory<string, PatientIdsAcquiredValue> _kafkaConsumerFactory;
        private readonly ITransientExceptionHandler<string, PatientIdsAcquiredValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, PatientIdsAcquiredValue> _deadLetterExceptionHandler;
        private readonly IMediator _mediator;

        private string Name => this.GetType().Name;

        public PatientIdsAcquiredListener(ILogger<PatientIdsAcquiredListener> logger, IKafkaConsumerFactory<string, PatientIdsAcquiredValue> kafkaConsumerFactory,
          ITransientExceptionHandler<string, PatientIdsAcquiredValue> transientExceptionHandler,
          IDeadLetterExceptionHandler<string, PatientIdsAcquiredValue> deadLetterExceptionHandler, IMediator mediator ) 
        { 
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(_transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(_deadLetterExceptionHandler));
            _mediator = mediator ?? throw new ArgumentException(nameof(mediator));
        }

        protected override System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return System.Threading.Tasks.Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var consumerConfig = new ConsumerConfig()
            {
                GroupId = ReportConstants.ServiceName,
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);

            try
            {
                consumer.Subscribe(nameof(KafkaTopic.PatientIDsAcquired));

                _logger.LogInformation($"Started PatientIdsAcquired consumer for topic '{nameof(KafkaTopic.PatientIDsAcquired)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    ConsumeResult<string, PatientIdsAcquiredValue>? consumeResult = null;
                    string facilityId = string.Empty;

                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                        {
                            try
                            {
                                consumeResult = result;

                                if (consumeResult == null)
                                {
                                    throw new DeadLetterException($"{Name}: consumeResult is null", AuditEventType.Create);
                                }

                                var key = consumeResult.Message.Key;
                                var value = consumeResult.Message.Value;
                                facilityId = key;

                                if (string.IsNullOrWhiteSpace(key))
                                {
                                    throw new DeadLetterException($"{Name}: key value is null or empty",
                                        AuditEventType.Create);
                                }

                                if (value.PatientIds == null)
                                {
                                    throw new DeadLetterException(
                                        $"{Name}: consumeResult.Value.PatientIds is null", AuditEventType.Create);
                                }

                                var scheduledReports = await _mediator.Send(
                                    new FindMeasureReportScheduleForFacilityQuery() { FacilityId = key },
                                    cancellationToken);

                                if (!scheduledReports?.Any() ?? false)
                                {
                                    throw new DeadLetterException(
                                        $"{Name}: No Scheduled Reports found for facilityId: {key}", AuditEventType.Query);
                                }

                                foreach (var scheduledReport in scheduledReports.Where(sr => !sr.PatientsToQueryDataRequested.GetValueOrDefault()))
                                {
                                    if (scheduledReport.PatientsToQuery == null)
                                    {
                                        scheduledReport.PatientsToQuery = new List<string>();
                                    }

                                    foreach (var patientId in value.PatientIds.Entry)
                                    {
                                        if (scheduledReport.PatientsToQuery.Contains(patientId.Item.Reference))
                                        {
                                            continue;
                                        }

                                        scheduledReport.PatientsToQuery.Add(patientId.Item.Reference);
                                    }

                                    try
                                    {
                                        await _mediator.Send(new UpdateMeasureReportScheduleCommand()
                                        {
                                            ReportSchedule = scheduledReport

                                        }, cancellationToken);
                                    }
                                    catch (Exception)
                                    {
                                        throw new TransientException("Failed to update ReportSchedule", AuditEventType.Create);
                                    }
                                }
                            }
                            catch (DeadLetterException ex)
                            {
                                _deadLetterExceptionHandler.HandleException(consumeResult, ex, facilityId);
                            }
                            catch (TransientException ex)
                            {
                                _transientExceptionHandler.HandleException(consumeResult, ex, facilityId);
                            }
                            catch (Exception ex)
                            {
                                _deadLetterExceptionHandler.HandleException(consumeResult, new DeadLetterException("Report - PatientIdsAcquired Exception thrown: " + ex.Message, AuditEventType.Create), facilityId);
                            }
                            finally 
                            {
                                consumer.Commit(consumeResult);
                            }


                        }, cancellationToken);
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming message.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumer loop cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message.");
            }
        }
    }
}
