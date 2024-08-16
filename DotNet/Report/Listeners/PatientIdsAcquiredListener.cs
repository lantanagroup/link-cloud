﻿using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;
using System.Text;

namespace LantanaGroup.Link.Report.Listeners
{
    public class PatientIdsAcquiredListener : BackgroundService
    {
        private readonly ILogger<PatientIdsAcquiredListener> _logger;
        private readonly IKafkaConsumerFactory<string, PatientIdsAcquiredValue> _kafkaConsumerFactory;
        private readonly ITransientExceptionHandler<string, PatientIdsAcquiredValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, PatientIdsAcquiredValue> _deadLetterExceptionHandler;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        private string Name => this.GetType().Name;

        public PatientIdsAcquiredListener(ILogger<PatientIdsAcquiredListener> logger, IKafkaConsumerFactory<string, PatientIdsAcquiredValue> kafkaConsumerFactory,
          ITransientExceptionHandler<string, PatientIdsAcquiredValue> transientExceptionHandler,
          IDeadLetterExceptionHandler<string, PatientIdsAcquiredValue> deadLetterExceptionHandler, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _serviceScopeFactory = serviceScopeFactory;


            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = KafkaTopic.PatientIDsAcquiredRetry.GetStringValue();

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.PatientIDsAcquired) + "-Error";
        }

        protected override System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return System.Threading.Tasks.Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
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
                consumer.Subscribe(nameof(KafkaTopic.PatientIDsAcquired));

                _logger.LogInformation($"Started PatientIdsAcquired consumer for topic '{nameof(KafkaTopic.PatientIDsAcquired)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    string facilityId = string.Empty;

                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            if (result == null)
                            {
                                consumer.Commit();
                                return;
                            }

                            try
                            {
                                var scope = _serviceScopeFactory.CreateScope();
                                var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

                                var key = result.Message.Key;
                                var value = result.Message.Value;
                                facilityId = key;

                                if (string.IsNullOrWhiteSpace(key) || value == null || value.PatientIds == null)
                                {
                                    throw new DeadLetterException("Invalid Patient Id's Acquired Event");
                                }

                                var scheduledReports =
                                    await database.ReportScheduledRepository.FindAsync(x =>
                                        x.FacilityId == key, cancellationToken);

                                if (!scheduledReports?.Any() ?? false)
                                {
                                    throw new TransientException(
                                        $"{Name}: No Scheduled Reports found for facilityId: {key}");
                                }

                                foreach (var scheduledReport in scheduledReports.Where(sr => !sr.PatientsToQueryDataRequested))
                                {
                                    if (scheduledReport.PatientsToQuery == null)
                                    {
                                        scheduledReport.PatientsToQuery = new List<string>();
                                    }

                                    foreach (var patientReference in value.PatientIds.Entry)
                                    {
                                        var patientId = patientReference.Item.Reference.Split('/').Last();
                                        if (scheduledReport.PatientsToQuery.Contains(patientId))
                                        {
                                            continue;
                                        }

                                        scheduledReport.PatientsToQuery.Add(patientId);
                                    }

                                    try
                                    {
                                        await database.ReportScheduledRepository.UpdateAsync(scheduledReport, consumeCancellationToken);
                                    }
                                    catch (Exception)
                                    {
                                        throw new TransientException("Failed to update ReportSchedule");
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
                            catch (Exception ex)
                            {
                                _deadLetterExceptionHandler.HandleException(result, new DeadLetterException("Report - PatientIdsAcquired Exception thrown: " + ex.Message), facilityId);
                            }
                            finally
                            {
                                consumer.Commit(result);
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
                        _logger.LogError(ex, "Error encountered in PatientIdsAcquiredListener");
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
