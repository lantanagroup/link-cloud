using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Application.Core;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.SerDes;
using System.Text;
using System.Text.Json;
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
                                await ProcessMessageAsync(result, facilityId, cancellationToken);
                            }
                            catch (DeadLetterException ex)
                            {
                                _deadLetterExceptionHandler.HandleException(result, ex, facilityId);
                            }
                            catch (TransientException ex)
                            {
                                _transientExceptionHandler.HandleException(result, ex, facilityId);
                            }
                            catch (Exception ex)
                            {
                                _deadLetterExceptionHandler.HandleException(result, new DeadLetterException("Report - MeasureReportGenerated Exception thrown", ex), facilityId);
                            }
                            finally
                            {
                                consumer.Commit(result);
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
                        consumer.Commit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset });
                    }
                    catch (Exception ex)
                    {
                        _exceptionLogger.Handle(ex, "Error encountered in MeasureReportGeneratedListener", LogLevel.Error);
                        consumer.Commit();
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
            {
                throw new DeadLetterException($"{Name}: MeasureReportGenerated event value segment missing");
            }

            if (!result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
            {
                throw new DeadLetterException($"{Name}: Received message without correlation ID (ReportId = {result.Message.Value.ReportTrackingId}, FacilityId = {result.Message.Value.FacilityId}).");
            }

            var messageValue = result.Message.Value;

            using var scope = _serviceScopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
            var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();
            var reportResourceManager = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();
            var reportPopulationManager = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();
            var patientAggregator = scope.ServiceProvider.GetRequiredService<PatientAggregator>();
            var reportManifestProducer = scope.ServiceProvider.GetRequiredService<ReportManifestProducer>();

            var correlationId = Encoding.UTF8.GetString(headerValue);

            var reportTrackingId = Guid.Parse(messageValue.ReportTrackingId);
            var schedule = await reportScheduledManager.GetReportSchedule(messageValue.FacilityId, reportTrackingId, cancellationToken);

            if (schedule == null)
            {
                throw new DeadLetterException($"{Name}: No scheduled report record was found (ReportId = {messageValue.ReportTrackingId}, FacilityId = {result.Message.Value.FacilityId}).");
            }

            var reportEntry = await reportEntryManager.UpdateAsyncWithConsumerResult(messageValue);

            var isAllNonReportable = reportEntry.MeasureReports.All(x => x.Status == Domain.Enums.MeasureReportStatus.NotReportable);

            if (isAllNonReportable)
            {
                await reportEntryManager.UpdateAsyncNotReportableEntry(reportEntry, cancellationToken);
                await reportManifestProducer.Produce(schedule, correlationId);
                return;
            }

            var readyForAggregation = reportEntry.MeasureReports.All(x => x.Status == Domain.Enums.MeasureReportStatus.NotReportable || x.Status == Domain.Enums.MeasureReportStatus.ReadyForValidation);

            if (!readyForAggregation)
            {
                await reportManifestProducer.Produce(schedule, correlationId);
                return;
            }

            AggregateResult aggregateResult;

            try
            {
                aggregateResult = await patientAggregator.AggregateToABS(messageValue.PatientId, schedule);
            }
            catch (Exception ex)
            {
                throw new DeadLetterException(ex.Message, ex);
            }

            await reportEntryManager.UpdateAsyncWithAggregateResult(reportEntry, aggregateResult);
            await reportResourceManager.AddAsyncWithAggregateResult(facilityId, reportTrackingId, messageValue.PatientId, aggregateResult, cancellationToken);

            // Bulk population handling (single DB load + bulk add for new populations)
            var existingPopulations = await reportPopulationManager.FindAsync(
                x => x.ReportScheduleId == reportTrackingId, cancellationToken);

            var existingDict = existingPopulations.ToDictionary(p => p.ReportType);

            var populationsToAdd = new List<ReportPopulationModel>();

            foreach (var aggregateMeasureReport in aggregateResult.MeasureReportResults)
            {
                if (existingDict.TryGetValue(aggregateMeasureReport.ReportType, out var populationModel))
                {
                    await reportPopulationManager.UpdateAsyncWithAggregateResult(populationModel, aggregateMeasureReport, cancellationToken);
                }
                else
                {
                    var newPopulation = new ReportPopulationModel
                    {
                        Id = Guid.NewGuid(),
                        Measure = aggregateMeasureReport.Measure,
                        ReportType = aggregateMeasureReport.ReportType,
                        CreateDate = DateTime.UtcNow,
                        FacilityId = facilityId,
                        ReportScheduleId = reportTrackingId
                    };

                    foreach (var measureReportpopulation in aggregateMeasureReport.PopulationList)
                    {
                        if (string.IsNullOrWhiteSpace(measureReportpopulation.PopulationId))
                            continue;

                        newPopulation.GroupPopulations.Add(new GroupPopulationModel
                        {
                            PopulationId = measureReportpopulation.PopulationId,
                            PopulationCodeJson = JsonSerializer.Serialize(measureReportpopulation.PopulationCode, LinkFhirSerializerOptions.ForFhirLenientSerialization),
                            TotalPopulationCount = measureReportpopulation.PopulationCount,
                            MeasureReportPopulations = new List<MeasureReportPopulationModel>
                            {
                                new MeasureReportPopulationModel
                                {
                                    MeasureReportId = aggregateMeasureReport.MeasureReportId,
                                    PopulationCount = measureReportpopulation.PopulationCount
                                }
                            }
                        });
                    }

                    populationsToAdd.Add(newPopulation);
                }
            }

            if (populationsToAdd.Count > 0)
            {
                await reportPopulationManager.AddRangeAsync(populationsToAdd, cancellationToken);
            }

            if (reportEntry.MeasureReports.All(x => x.Status == Domain.Enums.MeasureReportStatus.NotReportable))
            {
                await reportEntryManager.UpdateAsyncNotReportableEntry(reportEntry, cancellationToken);
                return;
            }

            try
            {
                await _readyForValidationProducer.Produce(schedule.Id, schedule.ReportTypes, schedule.FacilityId, messageValue.PatientId, aggregateResult.Uri.AbsoluteUri, correlationId);
            }
            catch (Exception ex)
            {
                _exceptionLogger.Handle(ex, "An error was encountered producing a ReadyForValidation", LogLevel.Error, facilityId, new { ReportId = schedule.Id });
                throw new DeadLetterException(ex.Message, ex);
            }
        }
    }
}