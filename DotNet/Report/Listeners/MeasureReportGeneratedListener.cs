using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Application.Core;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using System.Diagnostics;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Listeners
{
    public class MeasureReportGeneratedListener : BackgroundService
    {
        private readonly ILogger<MeasureReportGeneratedListener> _logger;
        private readonly IKafkaConsumerFactory<Null, MeasureReportGeneratedValue> _kafkaConsumerFactory;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        private readonly ITransientExceptionHandler<MeasureReportGeneratedListener, Null, MeasureReportGeneratedValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<MeasureReportGeneratedListener, Null, MeasureReportGeneratedValue> _deadLetterExceptionHandler;

        private readonly ReadyForValidationProducer _readyForValidationProducer;
        private readonly ServiceInformation _serviceInformation;

        private readonly IExceptionLogger<MeasureReportGeneratedListener> _exceptionLogger;

        private string Name => this.GetType().Name;

        public MeasureReportGeneratedListener(
            ILogger<MeasureReportGeneratedListener> logger,
            IKafkaConsumerFactory<Null, MeasureReportGeneratedValue> kafkaConsumerFactory,
            ITransientExceptionHandler<MeasureReportGeneratedListener, Null, MeasureReportGeneratedValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<MeasureReportGeneratedListener, Null, MeasureReportGeneratedValue> deadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory,
            ServiceInformation serviceInformation,
            ReadyForValidationProducer readyForValidationProducer,
            IExceptionLogger<MeasureReportGeneratedListener> exceptionLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));

            _serviceScopeFactory = serviceScopeFactory;

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.Topic = nameof(KafkaTopic.MeasureReportGenerated) + "-Retry";
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.MeasureReportGenerated) + "-Error";

            _readyForValidationProducer = readyForValidationProducer;
            _serviceInformation = serviceInformation;
            _exceptionLogger = exceptionLogger ?? throw new ArgumentNullException(nameof(exceptionLogger));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
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
                consumer.Subscribe(nameof(KafkaTopic.MeasureReportGenerated));
                _logger.LogInformation("{Name}: Started MeasureReportGenerated consumer on {date} for topic '{MeasureReportGeneratedName}'", Name, DateTime.UtcNow, nameof(KafkaTopic.MeasureReportGenerated));

                while (!cancellationToken.IsCancellationRequested)
                {
                    string facilityId = string.Empty;

                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            try
                            {
                                facilityId = result.Message.Value.FacilityId;
                                await ProcessMessageAsync(result, facilityId, consumeCancellationToken);
                            }
                            catch (DeadLetterException ex)
                            {
                                _deadLetterExceptionHandler.HandleException(result, ex, facilityId);
                            }
                            catch (TransientException ex)
                            {
                                _transientExceptionHandler.HandleException(result, ex, facilityId);
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                _deadLetterExceptionHandler.HandleException(result, new DeadLetterException("Report - MeasureReportGenerated Exception thrown", ex), facilityId);
                            }
                            finally
                            {
                                if (!consumeCancellationToken.IsCancellationRequested)
                                    consumer.SafeCommit(result, _logger);
                            }
                        }, cancellationToken);
                    }
                    catch (ConsumeException ex)
                    {
                        _exceptionLogger.Handle(ex, "Error consuming message for topics", LogLevel.Error, facilityId, new { Topics = string.Join(", ", consumer.Subscription) });

                        if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                        {
                            throw new OperationCanceledException(ex.Error.Reason, ex);
                        }

                        _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);

                        var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                        consumer.SafeCommit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset }, _logger);
                    }
                    catch (Exception ex)
                    {
                        _exceptionLogger.Handle(ex, "Error encountered in MeasureReportGeneratedListener", LogLevel.Error);
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

        public async Task ProcessMessageAsync(ConsumeResult<Null, MeasureReportGeneratedValue> result, string facilityId, CancellationToken cancellationToken)
        {
            if (result.Message.Value == null)
                throw new DeadLetterException($"{Name}: MeasureReportGenerated event value segment missing");

            if (!result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                throw new DeadLetterException($"{Name}: Received message without correlation ID (ReportId = {result.Message.Value.ReportTrackingId}, FacilityId = {result.Message.Value.FacilityId}).");

            var messageValue = result.Message.Value;
            var correlationId = Encoding.UTF8.GetString(headerValue);

            _logger.LogDebug("Consuming MeasureReportGenerated (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId}, ReportType = {ReportType})", messageValue.FacilityId, messageValue.PatientId, messageValue.ReportTrackingId, messageValue.ReportType);

            using var scope = _serviceScopeFactory.CreateScope();
            var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
            var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();
            var reportResourceManager = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();
            var reportPopulationManager = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();
            var patientAggregator = scope.ServiceProvider.GetRequiredService<PatientAggregator>();
            var reportManifestProducer = scope.ServiceProvider.GetRequiredService<ReportManifestProducer>();

            var reportTrackingId = Guid.Parse(messageValue.ReportTrackingId);
            var schedule = await reportScheduledManager.GetReportSchedule(messageValue.FacilityId, reportTrackingId, cancellationToken);

            if (schedule == null)
                throw new DeadLetterException($"{Name}: No scheduled report record was found (ReportId = {messageValue.ReportTrackingId}, FacilityId = {facilityId}).");

            var reportEntry = await reportEntryManager.UpdateAsyncWithConsumerResult(messageValue, cancellationToken);

            var isAllNonReportable = reportEntry.MeasureReports.All(x => x.Status == Domain.Enums.MeasureReportStatus.NotReportable);

            if (isAllNonReportable)
            {
                _logger.LogDebug("Entry not reportable (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", messageValue.FacilityId, messageValue.PatientId, messageValue.ReportTrackingId);

                await reportEntryManager.UpdateAsyncNotReportableEntry(reportEntry, cancellationToken);
                await reportManifestProducer.Produce(schedule, correlationId, cancellationToken);
                return;
            }

            //The aggregation step for a patient will only be performed once the Report service has consumed 'MeasureReportGenerated' events for all entries in reportEntry.MeasureReportList.
            var readyForAggregation = reportEntry.MeasureReports.All(x => x.Status == Domain.Enums.MeasureReportStatus.NotReportable || x.Status == Domain.Enums.MeasureReportStatus.ReadyForValidation);

            if (!readyForAggregation)
            {
                _logger.LogDebug("Patient not ready for aggregation (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", messageValue.FacilityId, messageValue.PatientId, messageValue.ReportTrackingId);
                //Daniel - 02/2026 - The 'isAllNonReportable' logic above was added and may replace the need for executing reportManifestProducer below. It won't hurt to run, but may not be needed. If we find that we don't need to execute the manifest producer, we will only need to return in this block.

                await reportManifestProducer.Produce(schedule, correlationId, cancellationToken);
                return;
            }

            _logger.LogDebug("Patient ready for aggregation (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", messageValue.FacilityId, messageValue.PatientId, messageValue.ReportTrackingId);
            var startTime = Stopwatch.GetTimestamp();

            AggregateResult aggregateResult;
            try
            {
                aggregateResult = await patientAggregator.AggregateToABS(messageValue.PatientId, schedule, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new DeadLetterException(ex.Message, ex);
            }

            var elapsed = Stopwatch.GetElapsedTime(startTime);
            _logger.LogDebug("Patient aggregation complete. Elapsed time: {Elapsed} (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", elapsed.ToString(), messageValue.FacilityId, messageValue.PatientId, messageValue.ReportTrackingId);
            startTime = Stopwatch.GetTimestamp();

            await reportEntryManager.UpdateAsyncWithAggregateResult(reportEntry, aggregateResult, cancellationToken);            

            foreach (var agg in aggregateResult.MeasureReportResults)
            {
                var existing = (await reportPopulationManager.FindAsync(
                    x => x.ReportScheduleId == reportTrackingId && x.ReportType == agg.ReportType, cancellationToken))
                    .FirstOrDefault();

                if (existing != null)
                {
                    await reportPopulationManager.UpdateAsyncWithAggregateResult(existing, agg, cancellationToken);
                }
                else
                {
                    await reportPopulationManager.AddAsyncWithAggregateResult(
                        facilityId, reportTrackingId, agg, cancellationToken);
                }
            }

            if (reportEntry.MeasureReports.All(x => x.Status == Domain.Enums.MeasureReportStatus.NotReportable))
            {
                await reportEntryManager.UpdateAsyncNotReportableEntry(reportEntry, cancellationToken);
                return;
            }

            elapsed = Stopwatch.GetElapsedTime(startTime);

            _logger.LogDebug("Database updates complete. Elapsed time: {Elapsed} (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", elapsed.ToString(), messageValue.FacilityId, messageValue.PatientId, messageValue.ReportTrackingId);
            
            try
            {
                await _readyForValidationProducer.Produce(schedule.Id, schedule.ReportTypes, schedule.FacilityId, messageValue.PatientId, aggregateResult.Uri.AbsoluteUri, correlationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _exceptionLogger.Handle(ex, "An error was encountered producing a ReadyForValidation", LogLevel.Error, facilityId, new { ReportId = schedule.Id });
                throw new DeadLetterException(ex.Message, ex);
            }
        }
    }
}