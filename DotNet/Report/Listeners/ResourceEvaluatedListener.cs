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
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Listeners
{
    public class ResourceEvaluatedListener : BackgroundService
    {

        private readonly ILogger<ResourceEvaluatedListener> _logger;
        private readonly IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue> _kafkaConsumerFactory;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        private readonly ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> _deadLetterExceptionHandler;

        private readonly ReadyForValidationProducer _readyForValidationProducer;
        private readonly SubmitReportProducer _submitReportProducer;

        private string Name => this.GetType().Name;

        public ResourceEvaluatedListener(
            ILogger<ResourceEvaluatedListener> logger, 
            IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue> kafkaConsumerFactory,
            ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue> deadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory,
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
                                var schedule = await measureReportScheduledManager.GetReportSchedule(key.FacilityId, value.ReportTrackingId, consumeCancellationToken) ??
                                            throw new TransientException($"{Name}: report schedule not found for Facility {key.FacilityId} and reportId: {value.ReportTrackingId}");


                                // find existing submission entry for this facility, report schedule, and patient
                                var entry = await submissionEntryManager.SingleAsync(e =>
                                    e.ReportScheduleId == schedule.Id
                                    && e.PatientId == value.PatientId
                                    && e.ReportType == value.ReportType, consumeCancellationToken);

                                // deserialize the resource
                                var resource = JsonSerializer.Deserialize<Resource>(value.Resource.ToString(),
                                    new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector,
                                        new FhirJsonPocoDeserializerSettings { Validator = null }));
                                
                                if (resource == null)
                                {
                                    throw new DeadLetterException($"{Name}: Unable to deserialize event resource");
                                }
                                
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
                                            // combine the meta profiles
                                            var existingProfiles = existingReportResource.GetResource().Meta?.Profile.ToList() ?? [];
                                            var newProfiles = resource.Meta?.Profile.ToList() ?? [];
                                            
                                            var profileSet = new HashSet<string>(existingProfiles);
                                            profileSet.UnionWith(newProfiles);
                                            
                                            _logger.LogInformation("Combining meta profiles for resource {ResourceId} with existing profiles: [{ExistingProfiles}] and new profiles: [{NewProfiles}].",
                                                resource.Id, string.Join(", ", existingProfiles), string.Join(", ", newProfiles));

                                            // update the existing resource meta profiles
                                            if (existingReportResource.GetResource().Meta == null)
                                            {
                                                existingReportResource.GetResource().Meta = new Meta { Profile = profileSet.ToList() };
                                            }
                                            else
                                            {
                                                existingReportResource.GetResource().Meta.Profile = profileSet.ToList();
                                            }
                                            
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
                                    
                                    if (resource.TypeName == "MeasureReport")
                                    {
                                        entry.AddMeasureReport((MeasureReport)resource);
                                    }
                                }

                                await submissionEntryManager.UpdateAsync(entry, cancellationToken);

                                if (entry.Status == PatientSubmissionStatus.ReadyForValidation && entry.ValidationStatus != ValidationStatus.Requested)
                                {
                                    await _readyForValidationProducer.Produce(schedule, entry);
                                }
                                else
                                {
                                    var allReady = !await submissionEntryManager.AnyAsync(e => e.FacilityId == schedule.FacilityId
                                                            && e.ReportScheduleId == schedule.Id
                                                            && e.Status != PatientSubmissionStatus.NotReportable
                                                            && e.Status != PatientSubmissionStatus.ValidationComplete, consumeCancellationToken);

                                    if (allReady)
                                    {
                                        await _submitReportProducer.Produce(schedule);
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
