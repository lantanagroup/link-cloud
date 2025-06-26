using System.Diagnostics;
using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Settings;
using System.Text;
using System.Text.Json;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Services.ResourceMerger;
using LantanaGroup.Link.Report.Services.ResourceMerger.Strategies;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using OpenTelemetry.Trace;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.Report.Core;

namespace LantanaGroup.Link.Report.Listeners
{
    public class ResourceEvaluatedListener : BackgroundService
    {

        private readonly ILogger<ResourceEvaluatedListener> _logger;
        private readonly IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue> _kafkaConsumerFactory;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        private readonly ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> _deadLetterExceptionHandler;

        private readonly PatientReportSubmissionBundler _patientReportSubmissionBundler;
        private readonly BlobStorageService _blobStorageService;
        private readonly ReadyForValidationProducer _readyForValidationProducer;
        private readonly SubmitReportProducer _submitReportProducer;

        private string Name => this.GetType().Name;

        public ResourceEvaluatedListener(
            ILogger<ResourceEvaluatedListener> logger, 
            IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue> kafkaConsumerFactory,
            ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> deadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory,
            PatientReportSubmissionBundler patientReportSubmissionBundler,
            BlobStorageService blobStorageService,
            ReadyForValidationProducer readyForValidationProducer,
            SubmitReportProducer submitReportProducer)
        {

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));

            _serviceScopeFactory = serviceScopeFactory;

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = nameof(KafkaTopic.ResourceEvaluated) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ResourceEvaluated) + "-Error";
            _patientReportSubmissionBundler = patientReportSubmissionBundler;
            _blobStorageService = blobStorageService;
            _readyForValidationProducer = readyForValidationProducer;
            _submitReportProducer = submitReportProducer;
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
                _logger.LogInformation("Started resource evaluated consumer on {date} for topic '{ResourceEvaluatedName}'", DateTime.UtcNow, nameof(KafkaTopic.ResourceEvaluated));

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
                                using var activity = ServiceActivitySource.Instance.StartActivity("ResourceEvaluatedListener.ExtractAndProcess");
                                
                                var key = result.Message.Key;
                                var value = result.Message.Value;
                                facilityId = key.FacilityId;

                                if (!result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                                {
                                    throw new DeadLetterException($"{Name}: Received message without correlation ID in topic: {result.Topic}, offset: {result.TopicPartitionOffset}");
                                }

                                if (value.Resource.ValueKind == JsonValueKind.Null || value.Resource.ValueKind == JsonValueKind.Undefined || (value.Resource.ValueKind == JsonValueKind.String && string.IsNullOrEmpty(value.Resource.GetString())))
                                {
                                    throw new DeadLetterException($"{Name}: Received message without a value in the resource property in topic: {result.Topic}, offset: {result.TopicPartitionOffset}");
                                }

                                var correlationIdStr = Encoding.UTF8.GetString(headerValue);
                                if(string.IsNullOrWhiteSpace(correlationIdStr))
                                {
                                    throw new DeadLetterException($"{Name}: Received message without correlation ID in topic: {result.Topic}, offset: {result.TopicPartitionOffset}");
                                }

                                if (string.IsNullOrWhiteSpace(key.FacilityId) || string.IsNullOrEmpty(value.ReportTrackingId))
                                {
                                    throw new DeadLetterException(
                                        $"{Name}: One or more required Key/Value properties are null, empty, or otherwise invalid.");
                                }

                                // find existing report scheduled for this facility, report type, and date range
                                activity?.AddEvent(new ActivityEvent("Find scheduled report", tags: [
                                        new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, key.FacilityId),
                                        new KeyValuePair<string, object?>(DiagnosticNames.ReportTrackingId, value.ReportTrackingId),
                                    ]) 
                                );
                                var schedule = await measureReportScheduledManager.GetReportSchedule(key.FacilityId, value.ReportTrackingId, consumeCancellationToken) ??
                                            throw new TransientException($"{Name}: report schedule not found for Facility {key.FacilityId} and reportId: {value.ReportTrackingId}");


                                // find existing submission entry for this facility, report schedule, and patient
                                activity?.AddEvent(new ActivityEvent("Find existing submission entry", tags: [
                                        new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, key.FacilityId),
                                        new KeyValuePair<string, object?>(DiagnosticNames.ReportScheduledId, schedule.Id),
                                        new KeyValuePair<string, object?>(DiagnosticNames.PatientId, value.PatientId),
                                        new KeyValuePair<string, object?>(DiagnosticNames.ReportType, value.ReportType),
                                    ]) 
                                );
                                var entry = await submissionEntryManager.SingleAsync(e =>
                                    e.ReportScheduleId == schedule.Id
                                    && e.PatientId == value.PatientId
                                    && e.ReportType == value.ReportType, consumeCancellationToken);

                                // deserialize the resource
                                activity?.AddEvent(new ActivityEvent("Deserialize resource"));
                                var resource = JsonSerializer.Deserialize<Resource>(value.Resource.ToString(),
                                    new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector,
                                        new FhirJsonPocoDeserializerSettings { Validator = null }));
                                
                                if (resource == null)
                                {
                                    throw new DeadLetterException($"{Name}: Unable to deserialize event resource");
                                }
                                
                                activity?.AddEvent(new ActivityEvent("Resource Deserialized", tags: [
                                        new KeyValuePair<string, object?>(DiagnosticNames.ResourceType, resource.TypeName),
                                        new KeyValuePair<string, object?>(DiagnosticNames.ResourceId, resource.Id),
                                        new KeyValuePair<string, object?>("reportable", value.IsReportable),
                                    ]) 
                                );
                                
                                if (value.IsReportable)
                                { 
                                    if (resource.TypeName == "MeasureReport")
                                    {
                                        entry.AddMeasureReport((MeasureReport)resource);
                                    }
                                    else
                                    {
                                        IFacilityResource? returnedResource = null;

                                        var existingReportResource =
                                            await resourceManager.GetResourceAsync(key.FacilityId, resource.Id, resource.TypeName, value.PatientId,
                                                consumeCancellationToken);

                                        if (existingReportResource != null)
                                        {
                                            // Set up the ResourceMerger with the UseLatestStrategy
                                            var merger = new ResourceMerger();
                                            var strategyLogger = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<ILogger<UseLatestStrategy>>();
                                            merger.SetStrategy(new UseLatestStrategy(strategyLogger));

                                            existingReportResource.SetResource(
                                                merger.Merge(existingReportResource.GetResource(), resource));
                                            
                                            // Update the existing resource using the merged version of the resource
                                            returnedResource =
                                                await resourceManager.UpdateResourceAsync(existingReportResource,
                                                    consumeCancellationToken);
                                            
                                            activity?.AddEvent(new ActivityEvent("Merge existing resource", tags: [
                                                    new KeyValuePair<string, object?>(DiagnosticNames.ResourceType, resource.TypeName),
                                                    new KeyValuePair<string, object?>(DiagnosticNames.ResourceId, resource.Id),
                                                    new KeyValuePair<string, object?>("merge.strategy", "UseLatestStrategy"),
                                                ]) 
                                            );
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
                                    
                                    if (resource.TypeName == "MeasureReport")
                                    {
                                        entry.AddMeasureReport((MeasureReport)resource);
                                    }
                                }

                                await submissionEntryManager.UpdateAsync(entry, consumeCancellationToken);

                                if (entry.Status == PatientSubmissionStatus.ReadyForValidation && entry.ValidationStatus != ValidationStatus.Requested)
                                {
                                    var patientSubmission = await _patientReportSubmissionBundler.GenerateBundle(facilityId, value.PatientId, schedule.Id);
                                    var payloadUri = await _blobStorageService.UploadAsync(schedule, patientSubmission, consumeCancellationToken);
                                    entry.PayloadUri = payloadUri?.ToString();
                                    await submissionEntryManager.UpdateAsync(entry, consumeCancellationToken);

                                    try
                                    {
                                        await _readyForValidationProducer.Produce(schedule, entry);
                                    }
                                    catch (ProduceException<ReadyForValidationKey, ReadyForValidationValue> ex)
                                    {
                                        _logger.LogError(ex, "An error was encountered generating a Ready For Validation event.\n\tFacilityId: {facilityId}\n\t", schedule.FacilityId);
                                    }
                                }
                                else
                                {
                                    var allReady = !await submissionEntryManager.AnyAsync(e => e.FacilityId == schedule.FacilityId
                                                            && e.ReportScheduleId == schedule.Id
                                                            && e.Status != PatientSubmissionStatus.NotReportable
                                                            && e.Status != PatientSubmissionStatus.ValidationComplete, consumeCancellationToken);
                                    
                                    activity?.AddEvent(new ActivityEvent("Check if ready for submission", tags: [
                                            new KeyValuePair<string, object?>("ready.for.submission", allReady),
                                        ]) 
                                    );

                                    if (allReady)
                                    {
                                        try
                                        {
                                            await _submitReportProducer.Produce(schedule);
                                        }
                                        catch (ProduceException<SubmitReportKey, SubmitReportValue> ex)
                                        {
                                            _logger.LogError(ex, "An error was encountered generating a Submit Report event.\n\tFacilityId: {facilityId}\n\t", schedule.FacilityId);
                                        }
                                    }
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
                                var exceptionMessage = $"Timeout exception encountered on {DateTime.UtcNow} for topics: [{string.Join(", ", consumer.Subscription)}] at offset: {result.TopicPartitionOffset}";
                                var transientException = new TransientException(exceptionMessage, ex);
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
                        _logger.LogError(ex, "Error consuming on {date} for topics: [{consumer}]", DateTime.UtcNow, string.Join(", ", consumer.Subscription));

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
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                Activity.Current?.RecordException(oce);
                
                _logger.LogError(oce, "Operation Canceled: {OceMessage}", oce.Message);
                consumer.Close();
                consumer.Dispose();
            }

        }

        private static string GetFacilityIdFromHeader(Headers headers)
        {
            var facilityId = string.Empty;

            if (headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionFacilityId, out var facilityIdBytes))
            {
                facilityId = Encoding.UTF8.GetString(facilityIdBytes);
            }

            return facilityId;
        }

    }
}
