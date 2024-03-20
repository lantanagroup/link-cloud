using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Commands;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using MediatR;
using Quartz;
using Quartz.Impl.Triggers;

namespace LantanaGroup.Link.Report.Listeners
{
    public class ReportScheduledListener : BackgroundService
    {

        private readonly ILogger<ReportScheduledListener> _logger;
        private readonly IKafkaConsumerFactory<MeasureReportScheduledKey, MeasureReportScheduledValue> _kafkaConsumerFactory;
        private readonly IMediator _mediator;
        private readonly ITransientExceptionHandler<MeasureReportScheduledKey, MeasureReportScheduledValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<MeasureReportScheduledKey, MeasureReportScheduledValue> _deadLetterExceptionHandler;
        private readonly ISchedulerFactory _schedulerFactory;


        public ReportScheduledListener(ILogger<ReportScheduledListener> logger, IKafkaConsumerFactory<MeasureReportScheduledKey, MeasureReportScheduledValue> kafkaConsumerFactory,
            IMediator mediator, ISchedulerFactory schedulerFactory,
            ITransientExceptionHandler<MeasureReportScheduledKey, MeasureReportScheduledValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<MeasureReportScheduledKey, MeasureReportScheduledValue> deadLetterExceptionHandler)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _schedulerFactory = schedulerFactory ?? throw new ArgumentException(nameof(schedulerFactory));
            _mediator = mediator ?? throw new ArgumentException(nameof(mediator));

            _transientExceptionHandler = transientExceptionHandler ??
                                               throw new ArgumentException(nameof(_deadLetterExceptionHandler));

            _deadLetterExceptionHandler = deadLetterExceptionHandler ??
                                               throw new ArgumentException(nameof(_deadLetterExceptionHandler));

            var t = (TransientExceptionHandler<MeasureReportScheduledKey, MeasureReportScheduledValue>)_transientExceptionHandler;
            t.ServiceName = "Report";
            t.Topic = nameof(KafkaTopic.ReportScheduled) + "-Retry";

            var d = (DeadLetterExceptionHandler<MeasureReportScheduledKey, MeasureReportScheduledValue>)_deadLetterExceptionHandler;
            d.ServiceName = "Report";
            d.Topic = nameof(KafkaTopic.ReportScheduled) + "-Error";
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }


        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig()
            {
                GroupId = "ReportScheduledEvent",
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(config);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.ReportScheduled));
                _logger.LogInformation($"Started report scheduled consumer for topic '{nameof(KafkaTopic.ReportScheduled)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var consumeResult = new ConsumeResult<MeasureReportScheduledKey, MeasureReportScheduledValue>();
                    try
                    {
                        consumeResult = consumer.Consume(cancellationToken);
                        if (consumeResult == null)
                        {
                            consumeResult = new ConsumeResult<MeasureReportScheduledKey, MeasureReportScheduledValue>();
                            throw new DeadLetterException(
                                "ReportSubmittedListener: Result of ConsumeResult<ReportSubmittedKey, ReportSubmittedValue>.Consume is null");
                        }

                        var key = consumeResult.Message.Key;
                        var value = consumeResult.Message.Value;

                        if (string.IsNullOrWhiteSpace(key.FacilityId) ||
                            string.IsNullOrWhiteSpace(key.ReportType))
                        {
                            throw new DeadLetterException(
                                "ReportScheduledListener: One or more required MeasureReportScheduledKey properties are null or empty.");
                        }

                        DateTimeOffset startDateOffset;
                        if (!DateTimeOffset.TryParse(
                                value.Parameters.Single(x => x.Key.ToLower() == "startdate").Value,
                                out startDateOffset))
                        {
                            throw new DeadLetterException("ReportScheduledListener: Start Date could not be parsed");
                        }

                        DateTimeOffset endDateOffset;
                        if (!DateTimeOffset.TryParse(
                                value.Parameters.Single(x => x.Key.ToLower() == "enddate").Value,
                                out endDateOffset))
                        {
                            throw new DeadLetterException("ReportScheduledListener: End Date could not be parsed");
                        }

                        var startDate = startDateOffset.UtcDateTime;
                        var endDate = endDateOffset.UtcDateTime;

                        var scheduleTrigger = "";
                        try
                        {
                            // There may eventually be a need to have the consumeResult.Message.Value contain a parameter indicating how often the job should run (Daily, Weekly, Monthly, etc)
                            // This will schedule the job to run once a month on the day, hour and minute specified on the endDate.
                            // However, when the job runs, it will delete itself from the schedule.
                            var cronSchedule =
                                CronScheduleBuilder
                                    .MonthlyOnDayAndHourAndMinute(endDate.Day, endDate.Hour, endDate.Minute)
                                    .Build() as CronTriggerImpl;
                            cronSchedule.StartTimeUtc = startDateOffset;
                            cronSchedule.EndTimeUtc = endDateOffset;
                            cronSchedule.SetNextFireTimeUtc(endDateOffset);

                            scheduleTrigger = cronSchedule.CronExpressionString;
                        }
                        catch (Exception ex)
                        {
                            throw new DeadLetterException(
                                "ReportScheduledListener: Cron Schedule could not be created from provided dates.", ex.InnerException);
                        }


                        if (string.IsNullOrWhiteSpace(scheduleTrigger))
                        {
                            throw new DeadLetterException(
                                "ReportScheduledListener: scheduleTrigger is null or empty.");
                        }

                        // create or update the consumed report schedule
                        var existing = await _mediator.Send(
                            new FindMeasureReportScheduleForReportTypeQuery()
                            {
                                FacilityId = key.FacilityId,
                                ReportStartDate = startDate,
                                ReportEndDate = endDate,
                                ReportType = key.ReportType
                            }, cancellationToken);

                        if (existing != null)
                        {
                            existing.FacilityId = key.FacilityId;
                            existing.ReportStartDate = startDate;
                            existing.ReportEndDate = endDate;
                            existing.ScheduledTrigger = scheduleTrigger;
                            existing.ReportType = key.ReportType;

                            await _mediator.Send(new UpdateMeasureReportScheduleCommand()
                            {
                                ReportSchedule = existing
                            }, cancellationToken);

                            if (existing.ScheduledTrigger != scheduleTrigger)
                            {
                                await MeasureReportScheduleService.RescheduleJob(existing,
                                    await _schedulerFactory.GetScheduler(cancellationToken));
                            }
                        }
                        else
                        {
                            var reportSchedule = await _mediator.Send(new CreateMeasureReportScheduleCommand
                            {
                                ReportSchedule = new MeasureReportScheduleModel
                                {
                                    FacilityId = key.FacilityId,
                                    ReportStartDate = startDate,
                                    ReportEndDate = endDate,
                                    ScheduledTrigger = scheduleTrigger,
                                    ReportType = key.ReportType
                                }
                            }, cancellationToken);

                            await MeasureReportScheduleService.CreateJobAndTrigger(reportSchedule,
                                await _schedulerFactory.GetScheduler(cancellationToken));


                            consumer.Commit(consumeResult);
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        consumer.Commit(consumeResult);
                        _deadLetterExceptionHandler.HandleException(consumeResult, new DeadLetterException("ReportScheduledListener: " + ex.Message, ex.InnerException));
                    }
                    catch (DeadLetterException ex)
                    {
                        consumer.Commit(consumeResult);
                        _deadLetterExceptionHandler.HandleException(consumeResult, ex);
                    }
                    catch (TransientException ex)
                    {
                        _transientExceptionHandler.HandleException(consumeResult, ex);
                        consumer.Commit(consumeResult);
                    }
                    catch (Exception ex)
                    {
                        consumer.Commit(consumeResult);
                        _deadLetterExceptionHandler.HandleException(consumeResult, new DeadLetterException("ReportScheduledListener: " + ex.Message, ex.InnerException));
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError($"Operation Canceled: {oce.Message}", oce);
                consumer.Close();
                consumer.Dispose();
            }

        }

    }
}
