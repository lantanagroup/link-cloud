using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Enums;
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
        private readonly ServiceInformation _serviceInformation;
        private readonly BlobStorageService _blobStorageService;

        public ReportScheduledListener(ILogger<ReportScheduledListener> logger, IKafkaConsumerFactory<string, ReportScheduledValue> kafkaConsumerFactory,
            ISchedulerFactory schedulerFactory,
            ITransientExceptionHandler<string, ReportScheduledValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<string, ReportScheduledValue> deadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory,
            BlobStorageService blobStorageService,
            ServiceInformation serviceInformation)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _schedulerFactory = schedulerFactory ?? throw new ArgumentException(nameof(schedulerFactory));
            _serviceScopeFactory = serviceScopeFactory;
            _serviceInformation = serviceInformation;

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.Topic = nameof(KafkaTopic.ReportScheduled) + "-Retry";
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ReportScheduled) + "-Error";

            _blobStorageService = blobStorageService;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig()
            {
                GroupId = _serviceInformation.ServiceConfigName,
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(config);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.ReportScheduled));
                _logger.LogInformation("Started report scheduled consumer for topic '{Topic}' at {StartTime}", nameof(KafkaTopic.ReportScheduled), DateTime.UtcNow);

                while (!cancellationToken.IsCancellationRequested)
                {
                    string facilityId = string.Empty;
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            await ProcessMessageAsync(result, consumeCancellationToken);
                            consumer.Commit(result);
                        }, cancellationToken);

                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming message for topics: [{Topics}] at {Timestamp}", string.Join(", ", consumer.Subscription), DateTime.UtcNow);

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
                _logger.LogError(oce, "Operation Canceled: {Message}", oce.Message);
                consumer.Close();
            }
        }

        public async Task ProcessMessageAsync(ConsumeResult<string, ReportScheduledValue> result, CancellationToken cancellationToken)
        {
            string facilityId = string.Empty;
            try
            {
                if (result == null)
                {
                    _logger.LogWarning("ReportScheduled event is null. Commiting and moving on.");
                    return;
                }

                var key = result.Message.Key;
                var value = result.Message.Value;

                if (!value.IsValid())
                {
                    throw new DeadLetterException("Invalid Report Scheduled event");
                }

                using var scope = _serviceScopeFactory.CreateScope();
                var reportScheduleManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
                var reportPopulationManager = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();

                facilityId = key;
                var startDate = value.StartDate.UtcDateTime;
                var endDate = value.EndDate.UtcDateTime;
                var frequency = value.Frequency;
                var reportTypes = value.ReportTypes;
                var reportId = value.ReportTrackingId;

                ReportSchedule? existing = null;

                if (!string.IsNullOrEmpty(reportId))
                {
                    existing = await reportScheduleManager.SingleOrDefaultAsync(x => x.Id == reportId, cancellationToken);
                }
                else
                {
                    reportId = Guid.NewGuid().ToString();
                }

                ReportSchedule? reportSchedule;
                if (existing != null)
                {
                    throw new DeadLetterException($"Report with id {reportId} already exists.");
                }
                else
                {
                    reportSchedule = new ReportSchedule
                    {
                        Id = reportId,
                        FacilityId = facilityId,
                        ReportStartDate = startDate,
                        ReportEndDate = endDate,
                        Frequency = frequency,
                        ReportTypes = reportTypes,
                        Status = ScheduleStatus.Scheduled,
                        CreateDate = DateTime.UtcNow
                    };
                    var reportName = _blobStorageService.GetReportName(reportSchedule);
                    reportSchedule.PayloadRootUri = _blobStorageService.GetUri(reportName)?.ToString();

                    reportSchedule = await reportScheduleManager.AddAsync(reportSchedule, cancellationToken);
                    await reportPopulationManager.AddWithReportScheduleAsync(reportSchedule, cancellationToken);

                    await MeasureReportScheduleService.CreateJobAndTrigger(reportSchedule, await _schedulerFactory.GetScheduler(cancellationToken));
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
                var exceptionMessage = $"Timeout exception encountered on {DateTime.UtcNow} for topics: [ReportScheduled] at offset: {result.TopicPartitionOffset}";
                var transientException = new TransientException(exceptionMessage, ex);
                _transientExceptionHandler.HandleException(result, transientException, facilityId);
            }
            catch (Exception ex)
            {
                _transientExceptionHandler.HandleException(result, ex, facilityId);
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