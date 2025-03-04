using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;
using System.Text;
using LantanaGroup.Link.Report.Domain.Enums;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Listeners
{
    public class ValidationCompleteListener : BackgroundService
    {

        private readonly ILogger<ValidationCompleteListener> _logger;
        private readonly IKafkaConsumerFactory<ValidationCompleteKey, ValidationCompleteValue> _kafkaConsumerFactory;
       
        private readonly IProducer<SubmitReportKey, SubmitReportValue> _submissionReportProducer;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        private readonly ITransientExceptionHandler<ValidationCompleteKey, ValidationCompleteValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<ValidationCompleteKey, ValidationCompleteValue> _deadLetterExceptionHandler;

        private readonly MeasureReportAggregator _aggregator;

        private string Name => this.GetType().Name;

        public ValidationCompleteListener(
            ILogger<ValidationCompleteListener> logger, 
            IKafkaConsumerFactory<ValidationCompleteKey, ValidationCompleteValue> kafkaConsumerFactory,
            ITransientExceptionHandler<ValidationCompleteKey, ValidationCompleteValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<ValidationCompleteKey, ValidationCompleteValue> deadLetterExceptionHandler,
            MeasureReportAggregator aggregator,
            IServiceScopeFactory serviceScopeFactory, 
            IProducer<SubmitReportKey, SubmitReportValue> submissionReportProducer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _aggregator = aggregator;

            _serviceScopeFactory = serviceScopeFactory;

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = nameof(KafkaTopic.ValidationComplete) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ValidationComplete) + "-Error";
            _submissionReportProducer = submissionReportProducer;
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

            ProducerConfig producerConfig = new ProducerConfig()
            {
                ClientId = "Report_ValidationComplete"
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.ValidationComplete));
                _logger.LogInformation($"Started resource evaluated consumer for topic '{nameof(KafkaTopic.ValidationComplete)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var facilityId = string.Empty;
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            if (result == null)
                            {
                                consumer.Commit();
                                return;
                            }

                            var scope = _serviceScopeFactory.CreateScope();
                            var measureReportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
                            var submissionEntryManager = scope.ServiceProvider.GetRequiredService<ISubmissionEntryManager>();

                            try
                            {
                                var key = result.Message.Key;
                                var value = result.Message.Value;
                                facilityId = key.FacilityId;

                                if (!result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                                {
                                    throw new DeadLetterException($"{Name}: Received message without correlation ID: {result.Topic}");
                                }

                                string CorrelationIdStr = Encoding.UTF8.GetString(headerValue);
                                if(string.IsNullOrWhiteSpace(CorrelationIdStr))
                                {
                                    throw new DeadLetterException($"{Name}: Received message without correlation ID: {result.Topic}");
                                }

                                var schedule = await measureReportScheduledManager.SingleOrDefaultAsync(s => s.Id == result.Message.Key.ReportId, cancellationToken);

                                if (schedule == null)
                                {
                                    throw new DeadLetterException(
                                        $"No ReportSchedule found for ID {result.Message.Key.ReportId}");
                                }

                                var submissionEntries =
                                    await submissionEntryManager.FindAsync(
                                        e => e.ReportScheduleId == schedule.Id && e.PatientId == value.PatientId, consumeCancellationToken);

                                foreach (var entry in submissionEntries)
                                {
                                    entry.ValidationStatus =
                                        value.IsValid ? ValidationStatus.Passed : ValidationStatus.Failed;

                                    entry.Status = PatientSubmissionStatus.ValidationComplete;

                                    await submissionEntryManager.UpdateAsync(entry, cancellationToken);
                                }


                                #region Patients To Query & Submision Report Handling

                                if (schedule.PatientsToQueryDataRequested)
                                {
                                    submissionEntries =
                                        await submissionEntryManager.FindAsync(e => e.FacilityId == schedule.FacilityId && e.ReportScheduleId == schedule.Id && e.Status != PatientSubmissionStatus.NotReportable, consumeCancellationToken);

                                    var allReady = submissionEntries.All(x => x.Status == PatientSubmissionStatus.ValidationComplete);

                                    if (allReady)
                                    {
                                        var patientIds = submissionEntries.Select(s => s.PatientId).ToList();

                                        List<MeasureReport?> measureReports = submissionEntries
                                            .Select(e => e.MeasureReport)
                                            .ToList();

                                        
                                        var organization = FhirHelperMethods.CreateOrganization(schedule.FacilityId, ReportConstants.BundleSettings.SubmittingOrganizationProfile, ReportConstants.BundleSettings.OrganizationTypeSystem,
                                            ReportConstants.BundleSettings.CdcOrgIdSystem, ReportConstants.BundleSettings.DataAbsentReasonExtensionUrl, ReportConstants.BundleSettings.DataAbsentReasonUnknownCode);
                                        
                                        _submissionReportProducer.Produce(nameof(KafkaTopic.SubmitReport),
                                            new Message<SubmitReportKey, SubmitReportValue>
                                            {
                                                Key = new SubmitReportKey()
                                                {
                                                    FacilityId = schedule.FacilityId,
                                                    StartDate = schedule.ReportStartDate,
                                                    EndDate = schedule.ReportEndDate
                                                },
                                                Value = new SubmitReportValue()
                                                {
                                                    PatientIds = patientIds,
                                                    MeasureIds = measureReports.Select(mr => mr.Measure).Distinct().ToList(),
                                                    Organization = organization,
                                                    Aggregates = _aggregator.Aggregate(measureReports, organization.Id, schedule.ReportStartDate, schedule.ReportEndDate)
                                                },
                                                Headers = new Headers
                                                {
                                                {
                                                    "X-Correlation-Id",
                                                    headerValue
                                                }
                                                }
                                            });

                                        _submissionReportProducer.Flush(consumeCancellationToken);

                                        
                                        schedule.SubmitReportDateTime = DateTime.UtcNow;
                                        await measureReportScheduledManager.UpdateAsync(schedule, consumeCancellationToken);

                                        foreach (var e in submissionEntries)
                                        {
                                            e.Status = PatientSubmissionStatus.Submitted;
                                            await submissionEntryManager.UpdateAsync(e, cancellationToken);
                                        }
                                    }
                                }

                                #endregion
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
                        _logger.LogError(ex, "Error encountered in ResourceEvaluatedListener");
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
