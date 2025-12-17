using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Services.ResourceMerger;
using LantanaGroup.Link.Report.Services.ResourceMerger.Strategies;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Settings;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
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

        private readonly ITransientExceptionHandler<Null, MeasureReportGeneratedValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<Null, MeasureReportGeneratedValue> _deadLetterExceptionHandler;

        private readonly PatientReportSubmissionBundler _patientReportSubmissionBundler;
        private readonly BlobStorageService _blobStorageService;
        private readonly ReadyForValidationProducer _readyForValidationProducer;
        private readonly ReportManifestProducer _reportManifestProducer;
        private readonly AuditableEventOccurredProducer _auditableEventOccurredProducer;
        private readonly IReportEntryStatusManager _reportEntryManager;
        private readonly IReportScheduledManager _reportScheduledManager;
        private readonly IReportPopulationManager _reportPopulationManager;

        private string Name => this.GetType().Name;

        public MeasureReportGeneratedListener(
            ILogger<MeasureReportGeneratedListener> logger,
            IKafkaConsumerFactory<Null, MeasureReportGeneratedValue> kafkaConsumerFactory,
            ITransientExceptionHandler<Null, MeasureReportGeneratedValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<Null, MeasureReportGeneratedValue> deadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory,
            PatientReportSubmissionBundler patientReportSubmissionBundler,
            BlobStorageService blobStorageService,
            ReadyForValidationProducer readyForValidationProducer,
            ReportManifestProducer reportManifestProducer,
            AuditableEventOccurredProducer auditableEventOccurredProducer, 
            IReportEntryStatusManager reportEntryManager,
            IReportScheduledManager reportScheduledManager,
            IReportPopulationManager reportPopulationManager)
        {

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));

            _serviceScopeFactory = serviceScopeFactory;

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = nameof(KafkaTopic.MeasureReportGenerated) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.MeasureReportGenerated) + "-Error";
            _patientReportSubmissionBundler = patientReportSubmissionBundler;
            _blobStorageService = blobStorageService;
            _readyForValidationProducer = readyForValidationProducer;
            _reportManifestProducer = reportManifestProducer;
            _auditableEventOccurredProducer = auditableEventOccurredProducer;
            _reportEntryManager = reportEntryManager;
            _reportScheduledManager = reportScheduledManager;
            _reportPopulationManager = reportPopulationManager;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async Task StartConsumerLoop(CancellationToken cancellationToken)
        {
            var consumerConfig = new ConsumerConfig()
            {
                GroupId = ReportConstants.ServiceName,
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.MeasureReportGenerated));
                _logger.LogInformation("Started MeasureReportGenerated consumer on {date} for topic '{MeasureReportGeneratedName}'", DateTime.UtcNow, nameof(KafkaTopic.MeasureReportGenerated));

                while (!cancellationToken.IsCancellationRequested)
                {
                    //TODO: Populate
                    string facilityId = string.Empty;

                    try
                    {
                        
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            try
                            {
                                if (!result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                                {
                                    throw new DeadLetterException("Correlation Id missing");
                                }

                                var correlationId = Encoding.UTF8.GetString(headerValue);

                                var reportEntry = await _reportEntryManager.GetEntry(result.Value.ReportTrackingId, result.Value.PatientId, cancellationToken);

                                if (reportEntry == null)
                                {
                                    //TODO: Throw error? Create an entry?
                                    throw new NotImplementedException();
                                }

                                MeasureReportEntry measureReportEntry = reportEntry.MeasureReportEntryList.First(x => x.ReportType == result.Value.ReportType);

                                measureReportEntry.MeasureReportId = result.Value.MeasureReportId;
                                measureReportEntry.MeasureReportFileName = result.Value.MeasureReportFileName;
                                measureReportEntry.MeasureReportUri = result.Value.MeasureReportURI;

                                if (result.Value.IsReportable)
                                {
                                    measureReportEntry.Status = MeasureReportStatus.ReadyForValidation;
                                }
                                else
                                {
                                    measureReportEntry.Status = MeasureReportStatus.NotReportable;
                                }

                                await _reportEntryManager.UpdateAsync(reportEntry, cancellationToken);

                                var schedule = await _reportScheduledManager.GetReportSchedule(result.Value.FacilityId, result.Value.ReportTrackingId, cancellationToken);

                                //TODO: Follow up on this logic
                                var readyForValidation = reportEntry.MeasureReportEntryList.All(x => x.Status == MeasureReportStatus.NotReportable || x.Status == MeasureReportStatus.ReadyForValidation);

                                if (!readyForValidation)
                                {
                                    await _reportManifestProducer.Produce(schedule, correlationId);
                                    return;
                                }

                                AggregateResult aggregateResult = await _patientReportSubmissionBundler.GenerateBundleToABS(result.Value.PatientId, result.Value.ReportTrackingId);

                                if (aggregateResult == null)
                                {
                                    throw new DeadLetterException($"No aggregated results were found for patient '{result.Value.PatientId}' for report id '{result.Value.ReportTrackingId}'");
                                }

                                reportEntry.AggregateReportUri = aggregateResult.Uri.AbsoluteUri;
                                reportEntry.AggregateReportFileName = aggregateResult.Uri.Segments.Last();
                                reportEntry.ModifyDate = DateTime.UtcNow;

                                await _reportEntryManager.UpdateAsync(reportEntry, cancellationToken);

                                foreach (var measureReportResult in aggregateResult.MeasureReportResults)
                                {
                                    var reportPopulationModel = await _reportPopulationManager.SingleOrDefaultAsync(x => x.ReportScheduleId == result.Value.ReportTrackingId && x.Measure == measureReportEntry.ReportType);

                                    if (reportPopulationModel == null)
                                    {
                                        reportPopulationModel = new ReportPopulationModel()
                                        {
                                            Measure = measureReportResult.Measure,
                                            CreateDate = DateTime.UtcNow,
                                            FacilityId = result.Value.FacilityId,
                                            ReportScheduleId = result.Value.ReportTrackingId
                                        };

                                        var serializer = new FhirJsonSerializer();

                                        foreach (var measureReportpopulation in measureReportResult.PopulationList)
                                        {
                                            ReportPopulation population = new ReportPopulation()
                                            {
                                                PopulationId = measureReportpopulation.PopulationId,
                                                PopulationCode = measureReportpopulation.PopulationCode,
                                                TotalPopulationCount = measureReportpopulation.PopulationCount,
                                                MeasureReportIds = new List<MeasureReportPopulation>() {
                                                new MeasureReportPopulation() {
                                                    MeasureReportId = measureReportResult.MeasureReportId,
                                                    PopulationCount = measureReportpopulation.PopulationCount
                                                }
                                            }
                                            };

                                            reportPopulationModel.ReportPopulations.Add(population);
                                        }

                                        await _reportPopulationManager.AddAsync(reportPopulationModel, cancellationToken);
                                    }
                                    else
                                    {
                                        //TODO: Add
                                    }
                                }

                                try
                                {
                                    await _readyForValidationProducer.Produce(schedule.Id, schedule.ReportTypes, schedule.FacilityId, result.Value.PatientId, aggregateResult.Uri.AbsolutePath, correlationId);
                                }
                                catch (ProduceException<ReadyForValidationKey, ReadyForValidationValue> ex)
                                {
                                    //TODO: Add logic
                                    //TODO: Test 
                                    _logger.LogError(ex, "An error was encountered generating a Ready For Validation event.\n\tFacilityId: {facilityId}\n\t", schedule.FacilityId);
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
                            catch (Exception ex)
                            {
                                _deadLetterExceptionHandler.HandleException(result, new DeadLetterException("Report - PatientListsAcquired Exception thrown: " + ex.Message), facilityId);
                            }
                            finally
                            {
                                consumer.Commit(result);
                            }
                        }, cancellationToken);
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming message for topics: [{Topics}] at {Timestamp}", string.Join(", ", consumer.Subscription), DateTime.UtcNow);

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
                        _logger.LogError(ex, "Error encountered in MeasureReportGeneratedListener");
                        consumer.Commit();
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError(oce, "Operation Canceled: {Message}", oce.Message);
                consumer.Close();
                consumer.Dispose();
            }
        }
    }
}
