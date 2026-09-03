using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using ReportingStatus = LantanaGroup.Link.Report.Domain.Enums.ReportingStatus;
using SubmissionStatus = LantanaGroup.Link.Report.Domain.Enums.SubmissionStatus;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;
using System.Text;

namespace LantanaGroup.Link.Report.Listeners
{
    public class PatientEventListener : BackgroundService
    {
        private readonly ILogger<PatientEventListener> _logger;
        private readonly IKafkaConsumerFactory<string, PatientEventValue> _kafkaConsumerFactory;
        private readonly ITransientExceptionHandler<PatientEventListener, string, PatientEventValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<PatientEventListener, string, PatientEventValue> _deadLetterExceptionHandler;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ServiceInformation _serviceInformation;

        private readonly IExceptionLogger<PatientEventListener> _exceptionLogger;

        private string Name => this.GetType().Name;

        public PatientEventListener(
            ILogger<PatientEventListener> logger,
            IKafkaConsumerFactory<string, PatientEventValue> kafkaConsumerFactory,
            ITransientExceptionHandler<PatientEventListener, string, PatientEventValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<PatientEventListener, string, PatientEventValue> deadLetterExceptionHandler,
            IExceptionLogger<PatientEventListener> exceptionLogger,
            IServiceScopeFactory serviceScopeFactory, ServiceInformation serviceInformation)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _serviceScopeFactory = serviceScopeFactory;
            _serviceInformation = serviceInformation;

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.Topic = KafkaTopic.PatientEventRetry.GetStringValue();
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.PatientEvent) + "-Error";
            _exceptionLogger = exceptionLogger ?? throw new ArgumentNullException(nameof(exceptionLogger));
        }

        protected override System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return System.Threading.Tasks.Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async Task StartConsumerLoop(CancellationToken cancellationToken)
        {
            var consumerConfig = new ConsumerConfig()
            {
                GroupId = _serviceInformation.ServiceConfigName,
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);

            try
            {
                consumer.Subscribe(nameof(KafkaTopic.PatientEvent));

                _logger.LogInformation("{Name}: Started PatientEvent consumer for topic '{Topic}' at {StartTime}", Name, nameof(KafkaTopic.PatientEvent), DateTime.UtcNow);

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
                        _exceptionLogger.Handle(ex, "Error encountered in PatientEventListener", LogLevel.Error);
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _exceptionLogger.Handle(oce, "Operation Canceled", LogLevel.Error);
                consumer.Close();
                consumer.Dispose();
            }
        }

        public async Task ProcessMessageAsync(ConsumeResult<string, PatientEventValue>? result, CancellationToken cancellationToken)
        {
            if (result == null)
            {
                _logger.LogWarning("Null PatientEvent consumer result found");
                return;
            }

            using var metricsMode = MetricsModeScope.Begin(KafkaHeaderHelper.IsPerformanceMode(result.Message?.Headers));
            string facilityId = result.Message.Key;

            try
            {
                if (result == null)
                {
                    return;
                }

                using var scope = _serviceScopeFactory.CreateScope();
                var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();
                var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

                var value = result.Message.Value;

                if (string.IsNullOrWhiteSpace(facilityId) || value == null)
                {
                    throw new DeadLetterException("Invalid Patient Event");
                }

                if (value.EventType != PatientEvents.Admit.ToString() && value.EventType != PatientEvents.Discharge.ToString())
                {
                    _logger.LogDebug("Patient {PatientId} has event type of {EventType}. Ignoring.", HtmlInputSanitizer.Sanitize(value.PatientId), HtmlInputSanitizer.Sanitize(value.EventType));
                    return;
                }

                _logger.LogDebug("Consuming {EventType} PatientEvent (FacilityId: {facilityId}, PatientId: {PatientId})", HtmlInputSanitizer.Sanitize(value.EventType), HtmlInputSanitizer.Sanitize(facilityId), HtmlInputSanitizer.Sanitize(value.PatientId));

                var scheduledReports = await reportScheduledManager.FindAsync(x => x.FacilityId == facilityId && x.EndOfReportPeriodJobHasRun == false, cancellationToken);

                if (scheduledReports == null || !scheduledReports.Any())
                {
                    throw new TransientException($"No Scheduled Reports found for facilityId: {facilityId}");
                }

                var newEntries = new List<ReportEntryModel>();
                var entriesToUpdate = new List<ReportEntryModel>();

                foreach (var scheduledReport in scheduledReports)
                {
                    var entry = await reportEntryManager.SingleOrDefaultAsync(e => e.ReportScheduleId == scheduledReport.Id && e.PatientId == value.PatientId, cancellationToken);

                    if (entry == null)
                    {
                        entry = new ReportEntryModel()
                        {
                            PatientId = value.PatientId,
                            FacilityId = scheduledReport.FacilityId,
                            CreateDate = DateTime.Now,
                            ReportingStatus = ReportingStatus.PatientIdentified,
                            ReportScheduleId = scheduledReport.Id
                        };
                        await reportEntryManager.AddAsync(entry, cancellationToken);
                    }

                    foreach (var reportType in scheduledReport.ReportTypes)
                    {
                        var measureReportEntry = entry.MeasureReports.Where(x => x.ReportType == reportType).FirstOrDefault();

                        if (measureReportEntry != null)
                        {
                            continue;
                        }

                        entry.MeasureReports.Add(new EntryMeasureReportModel()
                        {
                            ReportType = reportType
                        });

                        await reportEntryManager.UpdateAsync(entry, cancellationToken);
                    }
                }

                if (newEntries.Count > 0)
                {
                    await reportEntryManager.AddRangeAsync(newEntries, cancellationToken);
                }

                foreach (var entryToUpdate in entriesToUpdate)
                {
                    await reportEntryManager.UpdateAsync(entryToUpdate, cancellationToken);
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _deadLetterExceptionHandler.HandleException(result, new DeadLetterException("Report - PatientEvent Exception thrown: " + ex.Message), facilityId);
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