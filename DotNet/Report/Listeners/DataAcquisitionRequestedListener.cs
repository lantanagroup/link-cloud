using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace LantanaGroup.Link.Report.Listeners
{
    public class DataAcquisitionRequestedListener : BackgroundService
    {
        private readonly ILogger<DataAcquisitionRequestedListener> _logger;
        private readonly IKafkaConsumerFactory<string, DataAcquisitionRequestedValue> _kafkaConsumerFactory;
        private readonly ITransientExceptionHandler<string, DataAcquisitionRequestedValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, DataAcquisitionRequestedValue> _deadLetterExceptionHandler;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        private string Name => this.GetType().Name;

        public DataAcquisitionRequestedListener(
            ILogger<DataAcquisitionRequestedListener> logger, 
            IKafkaConsumerFactory<string, DataAcquisitionRequestedValue> kafkaConsumerFactory,
            ITransientExceptionHandler<string, DataAcquisitionRequestedValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<string, DataAcquisitionRequestedValue> deadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory) 
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _serviceScopeFactory = serviceScopeFactory;

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(_transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(_deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = KafkaTopic.DataAcquisitionRequestedRetry.GetStringValue();

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.DataAcquisitionRequested) + "-Error";
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

            using var consumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);

            try
            {
                consumer.Subscribe(nameof(KafkaTopic.DataAcquisitionRequested));

                _logger.LogInformation($"Started Data Acquisition Requested consumer for topic '{nameof(KafkaTopic.DataAcquisitionRequested)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    ConsumeResult<string, DataAcquisitionRequestedValue>? consumeResult = null;
                    string facilityId = string.Empty;

                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                        {
                            try
                            {
                                var scope = _serviceScopeFactory.CreateScope();
                                var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

                                consumeResult = result;

                                if (consumeResult == null)
                                {
                                    throw new DeadLetterException($"{Name}: consumeResult is null");
                                }

                                var key = consumeResult.Message.Key;
                                var value = consumeResult.Message.Value;
                                facilityId = key;

                                if (string.IsNullOrWhiteSpace(key) || value == null || value.ScheduledReports == null || !value.ScheduledReports.Any() || string.IsNullOrWhiteSpace(value.PatientId))
                                {
                                    throw new DeadLetterException("Invalid Data Acquisition Requested Event");
                                }

                                if (value.QueryType.ToLower() != QueryType.Initial.ToString().ToLower())
                                {
                                    return;
                                }

                                var scheduledReports =
                                    await database.ReportScheduledRepository.FindAsync(x =>
                                        x.FacilityId == key, cancellationToken);

                                if (!scheduledReports?.Any() ?? false)
                                {
                                    throw new DeadLetterException(
                                        $"{Name}: No Scheduled Reports found for facilityId: {key}");
                                }

                                foreach (var scheduledReport in scheduledReports.Where(sr => !sr.PatientsToQueryDataRequested))
                                {
                                    if (scheduledReport.PatientsToQuery == null)
                                    {
                                        continue;
                                    }

                                    scheduledReport.PatientsToQuery.Remove(value.PatientId);

                                    try
                                    {
                                        await database.ReportScheduledRepository.UpdateAsync(
                                            scheduledReport, cancellationToken);
                                    } 
                                    catch (Exception)
                                    {
                                        throw new TransientException("Failed to update ReportSchedule");
                                    }
                                }
                            }
                            catch (DeadLetterException ex)
                            {
                                _deadLetterExceptionHandler.HandleException(consumeResult, ex, facilityId);
                            }
                            catch (TransientException ex)
                            {
                                _transientExceptionHandler.HandleException(consumeResult, ex, facilityId);
                            }
                            catch (Exception ex)
                            {
                                _deadLetterExceptionHandler.HandleException(consumeResult, new DeadLetterException("Report - PatientIdsAcquired Exception thrown: " + ex.Message), facilityId);
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
                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogError(ex, $"Operation Canceled: {ex.Message}");
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
