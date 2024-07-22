using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Settings;
using System.Text;
using System.Text.Json;
using System.Transactions;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Listeners
{
    public class ResourceEvaluatedListener : BackgroundService
    {

        private readonly ILogger<ResourceEvaluatedListener> _logger;
        private readonly IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue> _kafkaConsumerFactory;
        private readonly IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue> _kafkaProducerFactory;
        private readonly IResourceManager _resourceManager;
        private readonly IMeasureReportScheduledManager _measureReportScheduledManager;
        private readonly ISubmissionEntryManager _submissionEntryManager;

        private readonly ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> _deadLetterExceptionHandler;

        private readonly MeasureReportSubmissionBundler _bundler;
        private readonly MeasureReportAggregator _aggregator;

        private string Name => this.GetType().Name;

        public ResourceEvaluatedListener(ILogger<ResourceEvaluatedListener> logger, IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue> kafkaConsumerFactory,
            IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue> kafkaProducerFactory,
            ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> deadLetterExceptionHandler,
            MeasureReportSubmissionBundler bundler,
            MeasureReportAggregator aggregator,
            IResourceManager resourceManager,
            IMeasureReportScheduledManager measureReportScheduledManager,
            ISubmissionEntryManager submissionEntryManager)
        {

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentException(nameof(kafkaProducerFactory));
            _bundler = bundler;
            _aggregator = aggregator;
            _resourceManager = resourceManager;
            _measureReportScheduledManager = measureReportScheduledManager;
            _submissionEntryManager = submissionEntryManager;

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = nameof(KafkaTopic.ResourceEvaluated) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ResourceEvaluated) + "-Error";
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
                    ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue>? consumeResult = null;
                    var facilityId = string.Empty;
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                        {
                            try
                            {
                                consumeResult = result;

                                if (consumeResult == null)
                                {
                                    throw new DeadLetterException($"{Name}: consumeResult is null");
                                }

                                var key = consumeResult.Message.Key;
                                var value = consumeResult.Message.Value;
                                facilityId = key.FacilityId;

                                if (!consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                                {
                                    throw new DeadLetterException($"{Name}: Received message without correlation ID: {consumeResult.Topic}");
                                }

                                string CorrelationIdStr = Encoding.UTF8.GetString(headerValue);
                                if(string.IsNullOrWhiteSpace(CorrelationIdStr))
                                {
                                    throw new DeadLetterException($"{Name}: Received message without correlation ID: {consumeResult.Topic}");
                                }

                                if (string.IsNullOrWhiteSpace(key.FacilityId) ||
                                    string.IsNullOrWhiteSpace(key.ReportType) ||
                                    key.StartDate == DateTime.MinValue ||
                                    key.EndDate == DateTime.MinValue)
                                {
                                    throw new DeadLetterException(
                                        $"{Name}: One or more required Key/Value properties are null, empty, or otherwise invalid.");
                                }

                                // find existing report scheduled for this facility, report type, and date range
                                var schedule = await _measureReportScheduledManager.GetMeasureReportSchedule(key.FacilityId, key.StartDate, key.EndDate, key.ReportType, cancellationToken) ??
                                            throw new TransactionException(
                                                $"{Name}: report schedule not found for Facility {key.FacilityId} and reporting period of {key.StartDate} - {key.EndDate} for {key.ReportType}");
                                

                                //TODO Find long term solution Daniel Vargas
                                if (value.IsReportable)
                                {
                                    var entry = (await _submissionEntryManager.SingleOrDefaultAsync(e =>
                                        e.MeasureReportScheduleId == schedule.Id
                                        && e.PatientId == value.PatientId, cancellationToken));

                                    if (entry == null)
                                    {
                                        entry = new MeasureReportSubmissionEntryModel
                                        {
                                            FacilityId = key.FacilityId,
                                            MeasureReportScheduleId = schedule.Id,
                                            PatientId = value.PatientId
                                        };
                                    }

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
                                            await _resourceManager.GetResourceAsync(key.FacilityId, resource.Id, resource.TypeName, value.PatientId,
                                                cancellationToken);

                                        if (existingReportResource != null)
                                        {
                                            returnedResource =
                                                await _resourceManager.UpdateResourceAsync(existingReportResource,
                                                    cancellationToken);
                                        }
                                        else
                                        {
                                            returnedResource = await _resourceManager.CreateResourceAsync(key.FacilityId, resource, value.PatientId, cancellationToken);
                                        }

                                        entry.UpdateContainedResource(returnedResource);
                                    }

                                    if (entry.Id == null)
                                    {
                                        await _submissionEntryManager.AddAsync(entry, cancellationToken);
                                    }
                                    else
                                    {
                                        await _submissionEntryManager.UpdateAsync(entry, cancellationToken);
                                    }
                                }

                                #region Patients To Query & Submision Report Handling

                                if (schedule.PatientsToQueryDataRequested)
                                {
                                    if (schedule.PatientsToQuery?.Contains(value.PatientId) ?? false)
                                    {
                                        schedule.PatientsToQuery.Remove(value.PatientId);

                                        await _measureReportScheduledManager.UpdateAsync(schedule, cancellationToken);
                                    }

                                    var submissionEntries =
                                        await _submissionEntryManager.FindAsync(
                                            e => e.MeasureReportScheduleId == schedule.Id, cancellationToken);

                                    var allReady = submissionEntries.All(x => x.ReadyForSubmission);

                                    if ((schedule.PatientsToQuery?.Count ?? 0) == 0 && allReady)
                                    {
                                        var patientIds = submissionEntries.Select(s => s.PatientId).ToList();

                                        List<MeasureReport?> measureReports = submissionEntries
                                            .Select(e => e.MeasureReport)
                                            .ToList();

                                        using var prod = _kafkaProducerFactory.CreateProducer(producerConfig);

                                        var organization = _bundler.CreateOrganization(schedule.FacilityId);
                                        prod.Produce(nameof(KafkaTopic.SubmitReport),
                                            new Message<SubmissionReportKey, SubmissionReportValue>
                                            {
                                                Key = new SubmissionReportKey()
                                                {
                                                    FacilityId = schedule.FacilityId,
                                                    StartDate = schedule.ReportStartDate,
                                                    EndDate = schedule.ReportEndDate
                                                },
                                                Value = new SubmissionReportValue()
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

                                        prod.Flush(cancellationToken);

                                        
                                        schedule.SubmitReportDateTime = DateTime.UtcNow;
                                        await _measureReportScheduledManager.UpdateAsync(schedule, cancellationToken);
                                    }
                                }

                                #endregion
                            }
                            catch (DeadLetterException ex)
                            {
                                _deadLetterExceptionHandler.HandleException(consumeResult, ex, facilityId);
                            }
                            catch (TransientException ex)
                            {
                                _transientExceptionHandler.HandleException(consumeResult, ex, facilityId);
                            }
                            catch (TimeoutException ex)
                            {
                                var transientException = new TransientException(ex.Message, ex.InnerException);

                                _transientExceptionHandler.HandleException(consumeResult, transientException, facilityId);
                            }
                            catch (Exception ex)
                            {
                                _deadLetterExceptionHandler.HandleException(ex, facilityId);
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

                        facilityId = GetFacilityIdFromHeader(ex.ConsumerRecord.Message.Headers);
                        var message = new Message<string, string>()
                        {
                            Headers = ex.ConsumerRecord.Message.Headers,
                            Key = ex.ConsumerRecord != null && ex.ConsumerRecord.Message != null &&
                                  ex.ConsumerRecord.Message.Key != null
                                ? Encoding.UTF8.GetString(ex.ConsumerRecord.Message.Key)
                                : string.Empty,
                            Value = ex.ConsumerRecord != null && ex.ConsumerRecord.Message != null &&
                                    ex.ConsumerRecord.Message.Value != null
                                ? Encoding.UTF8.GetString(ex.ConsumerRecord.Message.Value)
                                : string.Empty,
                        };

                        var dlEx = new DeadLetterException(ex.Message);
                        _deadLetterExceptionHandler.HandleException(message.Headers, message.Key, message.Value, dlEx, facilityId);
                        _logger.LogError(ex, "Error consuming message for topics: [{1}] at {2}", string.Join(", ", consumer.Subscription), DateTime.UtcNow);

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
                    catch (Exception ex)
                    {
                        _deadLetterExceptionHandler.HandleException(ex, facilityId);
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
