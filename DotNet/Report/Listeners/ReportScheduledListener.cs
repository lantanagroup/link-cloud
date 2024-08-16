﻿using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Settings;
using Quartz;
using System.Text;

namespace LantanaGroup.Link.Report.Listeners
{
    public class ReportScheduledListener : BackgroundService
    {

        private readonly ILogger<ReportScheduledListener> _logger;
        private readonly IKafkaConsumerFactory<MeasureReportScheduledKey, MeasureReportScheduledValue> _kafkaConsumerFactory;
        private readonly ITransientExceptionHandler<MeasureReportScheduledKey, MeasureReportScheduledValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<MeasureReportScheduledKey, MeasureReportScheduledValue> _deadLetterExceptionHandler;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        private string Name => this.GetType().Name;

        public ReportScheduledListener(ILogger<ReportScheduledListener> logger, IKafkaConsumerFactory<MeasureReportScheduledKey, MeasureReportScheduledValue> kafkaConsumerFactory,
            ISchedulerFactory schedulerFactory,
            ITransientExceptionHandler<MeasureReportScheduledKey, MeasureReportScheduledValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<MeasureReportScheduledKey, MeasureReportScheduledValue> deadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _schedulerFactory = schedulerFactory ?? throw new ArgumentException(nameof(schedulerFactory));
            _serviceScopeFactory = serviceScopeFactory;

            _transientExceptionHandler = transientExceptionHandler ??
                                               throw new ArgumentException(nameof(_deadLetterExceptionHandler));

            _deadLetterExceptionHandler = deadLetterExceptionHandler ??
                                               throw new ArgumentException(nameof(_deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = nameof(KafkaTopic.ReportScheduled) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ReportScheduled) + "-Error";
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
                consumer.Subscribe(nameof(KafkaTopic.ReportScheduled));
                _logger.LogInformation($"Started report scheduled consumer for topic '{nameof(KafkaTopic.ReportScheduled)}' at {DateTime.UtcNow}");

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
                                var measureReportScheduledManager =
                                    scope.ServiceProvider.GetRequiredService<IMeasureReportScheduledManager>();

                                var key = result.Message.Key;
                                var value = result.Message.Value;
                                facilityId = key.FacilityId;

                                if (string.IsNullOrWhiteSpace(key.FacilityId) ||
                                    string.IsNullOrWhiteSpace(key.ReportType))
                                {
                                    throw new DeadLetterException(
                                        $"{Name}: One or more required Key/Value properties are null or empty.");
                                }

                                DateTimeOffset startDateOffset;
                                if (!DateTimeOffset.TryParse(
                                        value.Parameters.Single(x => x.Key.ToLower() == "startdate").Value,
                                        out startDateOffset))
                                {
                                    throw new DeadLetterException($"{Name}: Start Date could not be parsed");
                                }

                                DateTimeOffset endDateOffset;
                                if (!DateTimeOffset.TryParse(
                                        value.Parameters.Single(x => x.Key.ToLower() == "enddate").Value,
                                        out endDateOffset))
                                {
                                    throw new DeadLetterException($"{Name}: End Date could not be parsed");
                                }

                                
                                var startDate = startDateOffset.UtcDateTime;
                                var endDate = endDateOffset.UtcDateTime;

                                // Check if this already exists
                                var existing = await measureReportScheduledManager.SingleOrDefaultAsync(x => x.FacilityId == facilityId 
                                                                                                        && x.ReportStartDate == startDate 
                                                                                                        && x.ReportEndDate == endDate 
                                                                                                        && x.ReportType == key.ReportType, consumeCancellationToken);

                                if (existing != null)
                                {
                                    throw new DeadLetterException(
                                        "MeasureReportScheduled data already exists for the provided FacilityId, ReportType, and Reporting period.");
                                }

                                var ent = new MeasureReportScheduleModel
                                {
                                    FacilityId = key.FacilityId,
                                    ReportStartDate = startDate,
                                    ReportEndDate = endDate,
                                    ReportType = key.ReportType,
                                    CreateDate = DateTime.UtcNow
                                };

                                var reportSchedule = await measureReportScheduledManager.AddAsync(ent, consumeCancellationToken);

                                await MeasureReportScheduleService.CreateJobAndTrigger(reportSchedule,
                                    await _schedulerFactory.GetScheduler(consumeCancellationToken));
                                
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
                        _logger.LogError(ex, "Error encountered in ReportScheduledListener");
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
