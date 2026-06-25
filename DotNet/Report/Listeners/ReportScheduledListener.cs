using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;
using System.Text;

namespace LantanaGroup.Link.Report.Listeners
{
    public class ReportScheduledListener : BackgroundService
    {
        private readonly ILogger<ReportScheduledListener> _logger;
        private readonly IKafkaConsumerFactory<string, ReportScheduledValue> _kafkaConsumerFactory;
        private readonly ITransientExceptionHandler<ReportScheduledListener, string, ReportScheduledValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<ReportScheduledListener, string, ReportScheduledValue> _deadLetterExceptionHandler;
        private readonly IQuartzJobHelper _quartzJobHelper;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ServiceInformation _serviceInformation;
        private readonly BlobStorageService _blobStorageService;

        private readonly IExceptionLogger<ReportScheduledListener> _exceptionLogger;

        public ReportScheduledListener(ILogger<ReportScheduledListener> logger,
            IKafkaConsumerFactory<string, ReportScheduledValue> kafkaConsumerFactory,
            IQuartzJobHelper quartzJobHelper,
            ITransientExceptionHandler<ReportScheduledListener, string, ReportScheduledValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<ReportScheduledListener, string, ReportScheduledValue> deadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory,
            BlobStorageService blobStorageService,
            ServiceInformation serviceInformation,
            IExceptionLogger<ReportScheduledListener> exceptionLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _quartzJobHelper = quartzJobHelper;
            _serviceScopeFactory = serviceScopeFactory;
            _serviceInformation = serviceInformation;

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.Topic = nameof(KafkaTopic.ReportScheduled) + "-Retry";
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ReportScheduled) + "-Error";

            _blobStorageService = blobStorageService;
            _exceptionLogger = exceptionLogger ?? throw new ArgumentNullException(nameof(exceptionLogger));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async Task StartConsumerLoop(CancellationToken cancellationToken)
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
                _logger.LogInformation("{Name}: Started consumer for topic '{Topic}' at {StartTime}", nameof(ReportScheduledListener), nameof(KafkaTopic.ReportScheduled), DateTime.UtcNow);

                while (!cancellationToken.IsCancellationRequested)
                {
                    string facilityId = string.Empty;
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            await ProcessMessageAsync(result, consumeCancellationToken);
                            consumer.SafeCommit(result, _logger);
                        }, cancellationToken);

                    }
                    catch (ConsumeException ex)
                    {
                        _exceptionLogger.Handle(ex, "Error consuming message for topics", LogLevel.Error, facilityId, new { Topics = string.Join(", ", consumer.Subscription) });

                        if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                        {
                            throw new OperationCanceledException(ex.Error.Reason, ex);
                        }

                        facilityId = GetFacilityIdFromHeader(ex.ConsumerRecord.Message.Headers);

                        _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);

                        var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                        consumer.SafeCommit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset }, _logger);
                    }
                    catch (Exception ex)
                    {
                        _exceptionLogger.Handle(ex, "Error encountered in ReportScheduledListener", LogLevel.Error);
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _exceptionLogger.Handle(oce, "Operation Canceled", LogLevel.Error);
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
                    throw new DeadLetterException("ReportScheduled event is null.");
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
                var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

                facilityId = key;
                var startDate = value.StartDate;
                var endDate = value.EndDate;
                var frequency = value.Frequency;
                var reportId = value.ReportTrackingId;

                var reportTypes = value.ReportTypes;

                ReportScheduleModel? existing = null;

                if (reportId != null)
                {
                    existing = await reportScheduleManager.SingleOrDefaultAsync(x => x.Id == reportId, cancellationToken);
                }
                else
                {
                    reportId = Guid.NewGuid();
                }

                if (existing != null)
                {
                    throw new DeadLetterException($"Report with id {reportId} already exists.");
                }

                var reportSchedule = new ReportScheduleModel
                {
                    Id = reportId.Value,
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

                await _quartzJobHelper.ScheduleJob<EndOfReportPeriodJob>(new Dictionary<string, object>
                {
                    { "ReportScheduleId", reportSchedule.Id },
                    { "FacilityId", reportSchedule.FacilityId }
                }, reportSchedule.ReportEndDate, reportSchedule.Id.ToString(), ReportConstants.MeasureReportSubmissionScheduler.Group, $"{reportSchedule.Id}-{reportSchedule.ReportEndDate}");
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
            catch (OperationCanceledException)
            {
                throw;
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