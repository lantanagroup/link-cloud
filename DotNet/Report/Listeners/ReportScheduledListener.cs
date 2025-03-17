using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Settings;
using Quartz;
using System.Text;

namespace LantanaGroup.Link.Report.Listeners
{
    public class ReportScheduledListener : BackgroundService
    {

        private readonly ILogger<ReportScheduledListener> _logger;
        private readonly IKafkaConsumerFactory<string, ReportScheduledValue> _kafkaConsumerFactory;
        private readonly ITransientExceptionHandler<string, ReportScheduledValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, ReportScheduledValue> _deadLetterExceptionHandler;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        private string Name => this.GetType().Name;

        public ReportScheduledListener(ILogger<ReportScheduledListener> logger, IKafkaConsumerFactory<string, ReportScheduledValue> kafkaConsumerFactory,
            ISchedulerFactory schedulerFactory,
            ITransientExceptionHandler<string, ReportScheduledValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<string, ReportScheduledValue> deadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _schedulerFactory = schedulerFactory ?? throw new ArgumentException(nameof(schedulerFactory));
            _serviceScopeFactory = serviceScopeFactory;

            _transientExceptionHandler = transientExceptionHandler ??
                                               throw new ArgumentException(nameof(_deadLetterExceptionHandler));

            _deadLetterExceptionHandler = deadLetterExceptionHandler ??
                                               throw new ArgumentException(nameof(_deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = nameof(KafkaTopic.ReportScheduled) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ReportScheduled) + "-Error";
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }


        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig()
            {
                GroupId = ReportConstants.ServiceName,
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(config);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.ReportScheduled));
                _logger.LogInformation($"Started report scheduled consumer for topic '{nameof(KafkaTopic.ReportScheduled)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    string facilityId = string.Empty;
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            if (result == null)
                            {
                                consumer.Commit();
                                return;
                            }

                            try
                            {
                                var key = result.Message.Key;
                                var value = result.Message.Value;

                                if (!value.IsValid())
                                {
                                    throw new DeadLetterException("Invalid Report Scheduled event");
                                }

                                var scope = _serviceScopeFactory.CreateScope();
                                var measureReportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

                                facilityId = key;
                                var startDate = value.StartDate.UtcDateTime;
                                var endDate = value.EndDate.UtcDateTime;
                                var frequency = value.Frequency;
                                var reportTypes = value.ReportTypes;
                                var reportId = value.ReportTrackingId;

                                // Check if this already exists
                                var existing = await measureReportScheduledManager.SingleOrDefaultAsync(x => x.Id == reportId, consumeCancellationToken);

                                ReportScheduleModel? reportSchedule;
                                if(existing != null) 
                                {
                                    reportSchedule = await measureReportScheduledManager.UpdateAsync(existing, consumeCancellationToken);

                                    await MeasureReportScheduleService.RescheduleJob(reportSchedule,
                                        await _schedulerFactory.GetScheduler(consumeCancellationToken));
                                }
                                else
                                {
                                    reportSchedule = new ReportScheduleModel
                                    {
                                        Id = reportId,
                                        FacilityId = facilityId,
                                        ReportStartDate = startDate,
                                        ReportEndDate = endDate,
                                        Frequency = frequency.ToString(),
                                        ReportTypes = reportTypes,
                                        CreateDate = DateTime.UtcNow
                                    };

                                    reportSchedule = await measureReportScheduledManager.AddAsync(reportSchedule, consumeCancellationToken);

                                    await MeasureReportScheduleService.CreateJobAndTrigger(reportSchedule,
                                        await _schedulerFactory.GetScheduler(consumeCancellationToken));
                                }
                            }
                            catch (DeadLetterException ex)
                            {
                                _deadLetterExceptionHandler.HandleException(result, ex, facilityId);
                            }
                            catch (TransientException ex)
                            {
                                _transientExceptionHandler.HandleException(result, ex, facilityId);
                            }
                            catch (TimeoutException ex)
                            {
                                var transientException = new TransientException(ex.Message, ex.InnerException);

                                _transientExceptionHandler.HandleException(result, transientException, facilityId);
                            }
                            catch (Exception ex)
                            {
                                _transientExceptionHandler.HandleException(result, ex, facilityId);
                            }
                            finally
                            {
                                consumer.Commit(result);
                            }
                        }, cancellationToken);

                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming message for topics: [{1}] at {2}", string.Join(", ", consumer.Subscription), DateTime.UtcNow);

                        if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                        {
                            throw new OperationCanceledException(ex.Error.Reason, ex);
                        }

                        facilityId = GetFacilityIdFromHeader(ex.ConsumerRecord.Message.Headers);

                        _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);

                        var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                        consumer.Commit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error encountered in ReportScheduledListener");
                        consumer.Commit();
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError(oce, $"Operation Canceled: {oce.Message}");
                consumer.Close();
                consumer.Dispose();
            }

        }

        private static string GetFacilityIdFromHeader(Headers headers)
        {
            string facilityId = string.Empty;

            if (headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionFacilityId, out var facilityIdBytes))
            {
                facilityId = Encoding.UTF8.GetString(facilityIdBytes);
            }

            return facilityId;
        }
    }
}
