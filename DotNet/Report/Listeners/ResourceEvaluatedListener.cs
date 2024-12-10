using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain.Enums;
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
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Listeners
{
    public class ResourceEvaluatedListener : BackgroundService
    {

        private readonly ILogger<ResourceEvaluatedListener> _logger;
        private readonly IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue> _kafkaConsumerFactory;
        private readonly IProducer<SubmitReportKey, SubmitReportValue> _submissionReportProducer;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        private readonly ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> _deadLetterExceptionHandler;

        private readonly PatientReportSubmissionBundler _bundler;
        private readonly MeasureReportAggregator _aggregator;

        private string Name => this.GetType().Name;

        public ResourceEvaluatedListener(
            ILogger<ResourceEvaluatedListener> logger, 
            IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue> kafkaConsumerFactory,
            ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> deadLetterExceptionHandler,
            PatientReportSubmissionBundler bundler,
            MeasureReportAggregator aggregator,
            IServiceScopeFactory serviceScopeFactory, 
            IProducer<SubmitReportKey, SubmitReportValue> submissionReportProducer)
        {

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _bundler = bundler;
            _aggregator = aggregator;

            _serviceScopeFactory = serviceScopeFactory;

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = nameof(KafkaTopic.ResourceEvaluated) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ResourceEvaluated) + "-Error";
            _submissionReportProducer = submissionReportProducer;
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

            ProducerConfig producerConfig = new ProducerConfig()
            {
                ClientId = "Report_SubmissionReportScheduled"
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.ResourceEvaluated));
                _logger.LogInformation($"Started resource evaluated consumer for topic '{nameof(KafkaTopic.ResourceEvaluated)}' at {DateTime.UtcNow}");

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
                            var resourceManager = scope.ServiceProvider.GetRequiredService<IResourceManager>();
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

                                if (string.IsNullOrWhiteSpace(key.FacilityId) ||
                                    string.IsNullOrWhiteSpace(value.ReportType) ||
                                    key.StartDate == DateTime.MinValue ||
                                    key.EndDate == DateTime.MinValue)
                                {
                                    throw new DeadLetterException(
                                        $"{Name}: One or more required Key/Value properties are null, empty, or otherwise invalid.");
                                }

                                // find existing report scheduled for this facility, report type, and date range
                                var schedule = await measureReportScheduledManager.GetReportSchedule(key.FacilityId, key.StartDate, key.EndDate, value.ReportType, consumeCancellationToken) ??
                                            throw new TransientException(
                                                $"{Name}: report schedule not found for Facility {key.FacilityId} and reporting period of {key.StartDate} - {key.EndDate} for {value.ReportType}");


                                var entry = await submissionEntryManager.SingleOrDefaultAsync(e =>
                                    e.ReportScheduleId == schedule.Id
                                    && e.PatientId == value.PatientId
                                    && e.ReportType == value.ReportType, consumeCancellationToken);

                                if (entry == null)
                                {
                                    entry = await submissionEntryManager.AddAsync(new MeasureReportSubmissionEntryModel()
                                    {
                                        PatientId = value.PatientId,
                                        Status = PatientSubmissionStatus.NotEvaluated,
                                        ReportScheduleId = schedule.Id,
                                        FacilityId = facilityId,
                                        ReportType = value.ReportType,
                                    });
                                }

                                if (value.IsReportable)
                                {
                                    var resource = JsonSerializer.Deserialize<Resource>(value.Resource.ToString(),
                                        new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector,
                                            new FhirJsonPocoDeserializerSettings { Validator = null }));

                                    if (resource == null)
                                    {
                                        throw new DeadLetterException($"{Name}: Unable to deserialize event resource");
                                    }

                                    if (resource.TypeName == "MeasureReport")
                                    {
                                        entry.AddMeasureReport((MeasureReport)resource);
                                    }
                                    else
                                    {
                                        IFacilityResource returnedResource = null;

                                        var existingReportResource =
                                            await resourceManager.GetResourceAsync(key.FacilityId, resource.Id, resource.TypeName, value.PatientId,
                                                consumeCancellationToken);

                                        if (existingReportResource != null)
                                        {
                                            returnedResource =
                                                await resourceManager.UpdateResourceAsync(existingReportResource,
                                                    consumeCancellationToken);
                                        }
                                        else
                                        {
                                            returnedResource = await resourceManager.CreateResourceAsync(key.FacilityId, resource, value.PatientId, consumeCancellationToken);
                                        }

                                        entry.UpdateContainedResource(returnedResource);
                                    }
                                }
                                else
                                {
                                    entry.Status = PatientSubmissionStatus.NotReportable;
                                }

                                if (entry.Status == PatientSubmissionStatus.ReadyForSubmission)
                                {
                                    entry.PatientSubmission = await _bundler.GenerateBundle(entry.FacilityId, entry.PatientId, entry.ReportScheduleId);
                                }

                                await submissionEntryManager.UpdateAsync(entry, consumeCancellationToken);

                                var entries = await submissionEntryManager.FindAsync(s =>
                                    s.FacilityId == entry.FacilityId
                                    && s.ReportScheduleId == entry.ReportScheduleId
                                    && s.Status != PatientSubmissionStatus.NotReportable, cancellationToken);

                                #region Submit Report Handling
                                if (schedule.PatientsToQueryDataRequested)
                                {
                                    var allReady = entries.All(x => x.Status == PatientSubmissionStatus.ReadyForSubmission);

                                    if (allReady)
                                    {
                                        var patientIds = entries.Select(s => s.PatientId).ToList();

                                        List<MeasureReport?> measureReports = entries
                                              .Select(e => e.MeasureReport)
                                              .Where(report => report != null)
                                              .ToList();

                                        var organization = FhirHelperMethods.CreateOrganization(schedule.FacilityId, ReportConstants.BundleSettings.SubmittingOrganizationProfile, ReportConstants.BundleSettings.OrganizationTypeSystem,
                                            ReportConstants.BundleSettings.CdcOrgIdSystem, ReportConstants.BundleSettings.DataAbsentReasonExtensionUrl, ReportConstants.BundleSettings.DataAbsentReasonUnknownCode);
                                        
                                        _submissionReportProducer.Produce(nameof(KafkaTopic.SubmitReport),
                                            new Message<SubmitReportKey, SubmitReportValue>
                                            {
                                                Key = new SubmitReportKey()
                                                {
                                                    FacilityId = schedule.FacilityId,
                                                    ReportScheduleId = schedule.Id,
                                                    StartDate = schedule.ReportStartDate,
                                                    EndDate = schedule.ReportEndDate
                                                },
                                                Value = new SubmitReportValue()
                                                {
                                                    PatientIds = patientIds,
                                                    MeasureIds = measureReports.Select(mr => mr.Measure).Distinct().ToList(),
                                                    Organization = organization,
                                                    Aggregates = _aggregator.Aggregate(measureReports, organization.Id, key.StartDate, key.EndDate)
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
