using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
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
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Listeners
{
    public class ValidationCompleteListener : BackgroundService
    {

        private readonly ILogger<ValidationCompleteListener> _logger;
        private readonly IKafkaConsumerFactory<string, ValidationCompleteValue> _kafkaConsumerFactory;      

        private readonly IServiceScopeFactory _serviceScopeFactory;

        private readonly ITransientExceptionHandler<string, ValidationCompleteValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, ValidationCompleteValue> _deadLetterExceptionHandler;

        private readonly SubmitReportProducer _submitReportProducer;

        private string Name => this.GetType().Name;

        public ValidationCompleteListener(
            ILogger<ValidationCompleteListener> logger, 
            IKafkaConsumerFactory<string, ValidationCompleteValue> kafkaConsumerFactory,
            ITransientExceptionHandler<string, ValidationCompleteValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<string, ValidationCompleteValue> deadLetterExceptionHandler,
            SubmitReportProducer submitReportProducer,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _submitReportProducer = submitReportProducer;

            _serviceScopeFactory = serviceScopeFactory;
            _submitReportProducer = submitReportProducer;

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = nameof(KafkaTopic.ValidationComplete) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ValidationComplete) + "-Error";
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
                _logger.LogInformation($"Started validation complete consumer for topic '{nameof(KafkaTopic.ValidationComplete)}' at {DateTime.UtcNow}");

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
                                var value = result.Message.Value;
                                facilityId = result.Message.Key;

                                var reportId = result.Message.Value.ReportTrackingId;

                                var schedule = await measureReportScheduledManager.SingleOrDefaultAsync(s => s.Id == reportId, cancellationToken);

                                if (schedule == null)
                                {
                                    throw new DeadLetterException(
                                        $"No ReportSchedule found for ID {reportId}");
                                }

                                var submissionEntries =
                                    await submissionEntryManager.FindAsync(
                                        e => e.ReportScheduleId == schedule.Id && e.PatientId == value.PatientId && e.Status == PatientSubmissionStatus.ValidationRequested, consumeCancellationToken);

                                foreach (var entry in submissionEntries)
                                {
                                    entry.ValidationStatus = value.IsValid ? ValidationStatus.Passed : ValidationStatus.Failed;

                                    entry.Status = PatientSubmissionStatus.ValidationComplete;

                                    await submissionEntryManager.UpdateAsync(entry, cancellationToken);
                                }

                                var allReady = !await submissionEntryManager.AnyAsync(e => e.FacilityId == schedule.FacilityId 
                                                                                        && e.ReportScheduleId == schedule.Id 
                                                                                        && e.Status != PatientSubmissionStatus.NotReportable 
                                                                                        && e.Status != PatientSubmissionStatus.ValidationComplete, consumeCancellationToken);

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
