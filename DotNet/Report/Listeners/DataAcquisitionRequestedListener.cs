using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Commands;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using MediatR;
using System.Threading;

namespace LantanaGroup.Link.Report.Listeners
{
    public class DataAcquisitionRequestedListener : BackgroundService
    {
        private readonly ILogger<DataAcquisitionRequestedListener> _logger;
        private readonly IKafkaConsumerFactory<string, DataAcquisitionRequestedValue> _kafkaConsumerFactory;
        private readonly ITransientExceptionHandler<string, DataAcquisitionRequestedValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, DataAcquisitionRequestedValue> _deadLetterExceptionHandler;
        private readonly IMediator _mediator;

        private string Name => this.GetType().Name;

        public DataAcquisitionRequestedListener(
            ILogger<DataAcquisitionRequestedListener> logger, 
            IKafkaConsumerFactory<string, DataAcquisitionRequestedValue> kafkaConsumerFactory,
            ITransientExceptionHandler<string, DataAcquisitionRequestedValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<string, DataAcquisitionRequestedValue> deadLetterExceptionHandler,
            IMediator mediator) 
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(_transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(_deadLetterExceptionHandler));
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
                GroupId = ReportConstants.ServiceName,
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);

            try
            {
                consumer.Subscribe(nameof(KafkaTopic.DataAcquisitionRequested));

                _logger.LogInformation($"Started Data Acquisition Requested consumer for topic '{nameof(KafkaTopic.DataAcquisitionRequested)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    ConsumeResult<string, DataAcquisitionRequestedValue>? consumeResult = null;
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

                                if (value == null)
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
                                        continue;
                                    }

                                    scheduledReport.PatientsToQuery.Remove(value.PatientId);

                                    await _mediator.Send(new UpdateMeasureReportScheduleCommand()
                                    {
                                        ReportSchedule = scheduledReport

                                    }, cancellationToken);
                                }
                            }
                            catch (DeadLetterException ex)
                            {
                                //TODO: Daniel - Add error handling
                                _logger.LogError(ex, "Error processing message.");
                                //await _deadLetterExceptionHandler.HandleException(consumeResult, ex, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                //TODO: Daniel - Add error handling
                                _logger.LogError(ex, "Error processing message.");
                            }


                        }, cancellationToken);
                    }
                    catch (DeadLetterException ex)
                    {
                        //TODO: Daniel - Add error handling
                        _logger.LogError(ex, "Error processing message.");
                        //await _deadLetterExceptionHandler.HandleException(consumeResult, ex, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        //TODO: Daniel - Add error handling
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
