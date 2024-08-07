﻿using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using QueryDispatch.Application.Settings;
using QueryDispatch.Domain.Managers;
using System.Text;

namespace LantanaGroup.Link.QueryDispatch.Listeners
{
    public class ReportScheduledEventListener : BackgroundService
    {
        private readonly ILogger<ReportScheduledEventListener> _logger;
        private readonly IKafkaConsumerFactory<ReportScheduledKey, ReportScheduledValue> _kafkaConsumerFactory;
        private readonly IQueryDispatchFactory _queryDispatchFactory;
        private readonly IProducer<string, AuditEventMessage> _auditProducer;
        private readonly IDeadLetterExceptionHandler<ReportScheduledKey, ReportScheduledValue> _deadLetterExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, string> _consumeResultDeadLetterExceptionHandler;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ReportScheduledEventListener(
            ILogger<ReportScheduledEventListener> logger,
            IKafkaConsumerFactory<ReportScheduledKey, ReportScheduledValue> kafkaConsumerFactory,
            IQueryDispatchFactory queryDispatchFactory, 
            IProducer<string, AuditEventMessage> auditProducer, 
            IDeadLetterExceptionHandler<ReportScheduledKey, ReportScheduledValue> deadLetterExceptionHandler,
            IDeadLetterExceptionHandler<string, string> consumeResultDeadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory) 
        {
            _logger = logger;
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _queryDispatchFactory = queryDispatchFactory;
            _auditProducer = auditProducer;
            _deadLetterExceptionHandler = deadLetterExceptionHandler;
            _consumeResultDeadLetterExceptionHandler = consumeResultDeadLetterExceptionHandler;
            _serviceScopeFactory = serviceScopeFactory;

            _deadLetterExceptionHandler.ServiceName = "QueryDispatch";
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.ReportScheduled) + "-Error";

            _consumeResultDeadLetterExceptionHandler.ServiceName = "QueryDispatch";
            _consumeResultDeadLetterExceptionHandler.Topic = nameof(KafkaTopic.ReportScheduled) + "-Error";
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken cancellationToken) {

            var config = new ConsumerConfig() { 
                GroupId = QueryDispatchConstants.ServiceName,
                EnableAutoCommit = false
            };

            using (var _reportScheduledConsumer = _kafkaConsumerFactory.CreateConsumer(config))
            {
                try
                {
                    _reportScheduledConsumer.Subscribe(nameof(KafkaTopic.ReportScheduled));
                    _logger.LogInformation("Started query dispatch consumer for topic '{reportScheduled}' at {date}", KafkaTopic.ReportScheduled, DateTime.UtcNow);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        ConsumeResult<ReportScheduledKey, ReportScheduledValue>? consumeResult;
                        try 
                        {
                            await _reportScheduledConsumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                            {
                                consumeResult = result;

                                try
                                {
                                    using var scope = _serviceScopeFactory.CreateScope();
                                
                                    var scheduledReportMgr = scope.ServiceProvider.GetRequiredService<IScheduledReportManager>();

                                    var scheduledReportRepo = scope.ServiceProvider.GetRequiredService<IEntityRepository<ScheduledReportEntity>>();

                                    if (consumeResult == null || !consumeResult.Message.Key.IsValid() || !consumeResult.Message.Value.IsValid())
                                    {
                                        throw new DeadLetterException("Invalid Report Scheduled event");
                                    }

                                    string correlationId = string.Empty;

                                    if (consumeResult.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                                    {
                                        correlationId = Encoding.UTF8.GetString(headerValue);
                                    }
                                    else
                                    {
                                        throw new DeadLetterException("Correlation Id missing");
                                    }

                                    ReportScheduledKey key = consumeResult.Message.Key;
                                    ReportScheduledValue value = consumeResult.Message.Value;

                                    // Validate the start and end dates
                                    if (!DateTimeOffset.TryParse(
                                            value.Parameters.Single(x => x.Key.Equals("startdate", StringComparison.CurrentCultureIgnoreCase)).Value,
                                            out DateTimeOffset startDateOffset))
                                    {
                                        throw new DeadLetterException($"{key.ReportType} report start date is missing or improperly formatted for Facility {key.FacilityId}");
                                    }

                                    if (!DateTimeOffset.TryParse(
                                            value.Parameters.Single(x => x.Key.Equals("enddate", StringComparison.CurrentCultureIgnoreCase)).Value,
                                            out DateTimeOffset endDateOffset))
                                    {
                                        throw new DeadLetterException($"{key.ReportType} report end date is missing or improperly formatted for Facility {key.FacilityId}");
                                    }

                                    var startDate = startDateOffset.UtcDateTime;
                                    var endDate = endDateOffset.UtcDateTime;           

                                    _logger.LogInformation("Consumed Event for: Facility '{FacilityId}' has a report type of '{ReportType}' with a report period of {startDate} to {endDate}", key.FacilityId, key.ReportType, startDate, endDate);

                                    var existingRecord = await scheduledReportRepo.FirstOrDefaultAsync(x => x.FacilityId == key.FacilityId);

                                    if (existingRecord != null)
                                    {
                                        _logger.LogInformation("Facility {facilityId} found", key.FacilityId);
                                        ScheduledReportEntity scheduledReport = _queryDispatchFactory.CreateScheduledReport(key.FacilityId, key.ReportType, startDate, endDate, correlationId);
                                        await scheduledReportMgr.UpdateScheduledReport(existingRecord, scheduledReport);
                                    }
                                    else
                                    {
                                        ScheduledReportEntity scheduledReport = _queryDispatchFactory.CreateScheduledReport(key.FacilityId, key.ReportType, startDate, endDate, correlationId);
                                       await scheduledReportMgr.createScheduledReport(scheduledReport);
                                    }

                                    _reportScheduledConsumer.Commit(consumeResult);

                                }
                                catch (DeadLetterException ex)
                                {
                                    _deadLetterExceptionHandler.HandleException(consumeResult, ex, consumeResult.Key.FacilityId);
                                    _reportScheduledConsumer.Commit(consumeResult);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, $"Failed to process Report Scheduled event.");

                                    var auditValue = new AuditEventMessage
                                    {
                                        FacilityId = consumeResult.Message.Key.FacilityId,
                                        Action = AuditEventType.Query,
                                        ServiceName = "QueryDispatch",
                                        EventDate = DateTime.UtcNow,
                                        Notes = $"Report Scheduled event processing failure \nException Message: {ex}",
                                    };

                                    ProduceAuditEvent(auditValue, consumeResult.Message.Headers);

                                    _deadLetterExceptionHandler.HandleException(consumeResult, new DeadLetterException("Query Dispatch Exception thrown: " + ex.Message), consumeResult.Message.Key.FacilityId);

                                    _reportScheduledConsumer.Commit(consumeResult);

                                    //continue;
                                }

                            }, cancellationToken);
                        }
                        catch (ConsumeException e)
                        {
                            if (e.Error.Code == ErrorCode.UnknownTopicOrPart)
                            {
                                throw new OperationCanceledException(e.Error.Reason, e);
                            }

                            var converted_record = new ConsumeResult<string, string>()
                            {
                                Message = new Message<string, string>()
                                {
                                    Key = e.ConsumerRecord.Message.Key != null ? Encoding.UTF8.GetString(e.ConsumerRecord.Message.Key) : "",
                                    Value = e.ConsumerRecord.Message.Value != null ? Encoding.UTF8.GetString(e.ConsumerRecord.Message.Value) : "",
                                    Headers = e.ConsumerRecord.Message.Headers
                                }
                            };

                            _consumeResultDeadLetterExceptionHandler.HandleException(converted_record, new DeadLetterException("Consume Result exception: " + e.InnerException.Message), string.Empty);

                            _reportScheduledConsumer.Commit();

                            continue;
                        }
                        
                    }
                    _reportScheduledConsumer.Close();
                    _reportScheduledConsumer.Dispose();
                }
                catch (OperationCanceledException oce)
                {
                    _logger.LogError(oce, $"Operation Canceled: {oce.Message}");
                    _reportScheduledConsumer.Close();
                    _reportScheduledConsumer.Dispose();
                }
            }
        }

        private void ProduceAuditEvent(AuditEventMessage auditEvent, Headers headers)
        {
                _auditProducer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                {
                    Value = auditEvent,
                    Headers = headers
                });
            
        }
    }
}
