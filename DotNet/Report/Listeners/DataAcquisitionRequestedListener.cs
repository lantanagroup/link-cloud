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
using LantanaGroup.Link.Shared.Application.Utilities;
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
            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = KafkaTopic.DataAcquisitionRequestedRetry.GetStringValue();

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.DataAcquisitionRequested) + "-Error";
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

                                if (string.IsNullOrWhiteSpace(key) || value == null || value.ScheduledReports == null || string.IsNullOrWhiteSpace(value.PatientId))
                                {
                                    throw new DeadLetterException("Invalid Data Acquisition Requested Event", AuditEventType.Create);
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
                        if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                        {
                            throw new OperationCanceledException(ex.Error.Reason, ex);
                        }

                        _deadLetterExceptionHandler.HandleException(new DeadLetterException($"{Name}: " + ex.Message, AuditEventType.Create, ex.InnerException), facilityId);

                        TopicPartitionOffset? offset = ex.ConsumerRecord?.TopicPartitionOffset;
                        if (offset == null)
                        {
                            consumer.Commit();
                        }
                        else
                        {
                            consumer.Commit(new List<TopicPartitionOffset> { offset });
                        }
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogError(ex, $"Operation Canceled: {ex.Message}");
                consumer.Close();
                consumer.Dispose();
            }
        }
    }
}
