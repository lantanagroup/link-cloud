using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace LantanaGroup.Link.Report.Listeners
{
    public class ReportSubmittedListener : BackgroundService
    {

        private readonly ILogger<ReportSubmittedListener> _logger;
        private readonly IKafkaConsumerFactory<ReportSubmittedKey, ReportSubmittedValue> _kafkaConsumerFactory;
        //Doesn't actually generate a SubmissionReport but prevents us from having to declare a new factory type in the initialization
        //for a ProducerFactory that's only used for an AuditEvent
        private readonly IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue> _kafkaProducerFactory;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        private readonly ITransientExceptionHandler<ReportSubmittedKey, ReportSubmittedValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<ReportSubmittedKey, ReportSubmittedValue> _deadLetterExceptionHandler;

        private string Name => this.GetType().Name;

        public ReportSubmittedListener(ILogger<ReportSubmittedListener> logger, IKafkaConsumerFactory<ReportSubmittedKey, ReportSubmittedValue> kafkaConsumerFactory,
            IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue> kafkaProducerFactory, IServiceScopeFactory serviceScopeFactory,
            ITransientExceptionHandler<ReportSubmittedKey, ReportSubmittedValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<ReportSubmittedKey, ReportSubmittedValue> deadLetterExceptionHandler)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentException(nameof(kafkaProducerFactory));
            _serviceScopeFactory = serviceScopeFactory;

            _transientExceptionHandler = transientExceptionHandler ??
                                               throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _deadLetterExceptionHandler = deadLetterExceptionHandler ??
                                      throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = nameof(KafkaTopic.ReportSubmitted) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ReportSubmitted) + "-Error";
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }


        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig()
            {
                GroupId = ReportConstants.ServiceName,
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(config);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.ReportSubmitted));
                _logger.LogInformation($"Started report submitted consumer for topic '{nameof(KafkaTopic.ReportSubmitted)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var consumeResult = new ConsumeResult<ReportSubmittedKey, ReportSubmittedValue>();
                    string facilityId = string.Empty;
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                        {
                            consumeResult = result;

                            try
                            {
                                var scope = _serviceScopeFactory.CreateScope();
                                var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

                                if (consumeResult == null)
                                {
                                    throw new DeadLetterException(
                                        $"{Name}: consumeResult is null");
                                }

                                var key = consumeResult.Message.Key;
                                var value = consumeResult.Message.Value;
                                facilityId = key.FacilityId;

                                if (string.IsNullOrWhiteSpace(key?.FacilityId))
                                {
                                    throw new DeadLetterException("FacilityId is null or empty");
                                }

                                if (string.IsNullOrWhiteSpace(value?.ReportBundleId))
                                {
                                    throw new DeadLetterException("ReportBundleId is null or empty");
                                }

                                // find existing report schedule
                                var subEntry = await database.ReportSubmissionRepository.SingleAsync(e =>
                                        e.SubmissionBundle.Id == value.ReportBundleId, cancellationToken);

                                var schedule = await database.ReportScheduledRepository.SingleOrDefaultAsync(s =>
                                    s.Id == subEntry.MeasureReportScheduleId, cancellationToken);

                                if (schedule is null)
                                {
                                    throw new TransientException(
                                        $"{Name}: No report schedule found for submission bundle with ID {value.ReportBundleId}");
                                }

                                // update report schedule with submitted date
                                schedule.SubmittedDate = DateTime.UtcNow;
                                await database.ReportScheduledRepository.UpdateAsync(schedule, cancellationToken);

                                // produce audit message signalling the report service acknowledged the report has been submitted
                                using var producer = _kafkaProducerFactory.CreateAuditEventProducer();

                                string notes =
                                    $"{ReportConstants.ServiceName} has processed the {nameof(KafkaTopic.ReportSubmitted)} event for report bundle with ID {value.ReportBundleId} with report schedule ID {schedule.Id}";
                                var val = new AuditEventMessage
                                {
                                    FacilityId = schedule.FacilityId,
                                    ServiceName = ReportConstants.ServiceName,
                                    Action = AuditEventType.Submit,
                                    EventDate = DateTime.UtcNow,
                                    Resource = typeof(MeasureReportScheduleModel).Name,
                                    Notes = notes
                                };
                                var headers = new Headers
                                {
                                    { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) }
                                };

                                producer.Produce(nameof(KafkaTopic.AuditableEventOccurred),
                                    new Message<string, AuditEventMessage>
                                    {
                                        Value = val,
                                        Headers = headers
                                    });
                                producer.Flush();

                                _logger.LogInformation(notes);
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
