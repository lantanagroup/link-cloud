using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Commands;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
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

        private string Name => this.GetType().Name;

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
                    ConsumeResult<MeasureReportScheduledKey, MeasureReportScheduledValue>? consumeResult = null;
                    string facilityId = string.Empty;
                    try
                    {
                        consumeResult = consumer.Consume(cancellationToken);
                        if (consumeResult == null)
                        {
                            throw new DeadLetterException(
                                $"{Name}: consumeResult is null", AuditEventType.Create);
                        }

                        var key = consumeResult.Message.Key;
                        var value = consumeResult.Message.Value;
                        facilityId = key.FacilityId;

                        if (string.IsNullOrWhiteSpace(key.FacilityId) ||
                            string.IsNullOrWhiteSpace(key.ReportType))
                        {
                            throw new DeadLetterException(
                                $"{Name}: One or more required Key/Value properties are null or empty.", AuditEventType.Create);
                        }

                        DateTimeOffset startDateOffset;
                        if (!DateTimeOffset.TryParse(
                                value.Parameters.Single(x => x.Key.ToLower() == "startdate").Value,
                                out startDateOffset))
                        {
                            throw new DeadLetterException($"{Name}: Start Date could not be parsed", AuditEventType.Create);
                        }

                        DateTimeOffset endDateOffset;
                        if (!DateTimeOffset.TryParse(
                                value.Parameters.Single(x => x.Key.ToLower() == "enddate").Value,
                                out endDateOffset))
                        {
                            throw new DeadLetterException($"{Name}: End Date could not be parsed", AuditEventType.Create);
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
                                "ReportScheduledListener: Cron Schedule could not be created from provided dates.", AuditEventType.Create, ex.InnerException);
                        }


                        if (string.IsNullOrWhiteSpace(scheduleTrigger))
                        {
                            throw new DeadLetterException(
                                "ReportScheduledListener: scheduleTrigger is null or empty.", AuditEventType.Create);
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
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        _deadLetterExceptionHandler.HandleException(consumeResult,
                            new DeadLetterException($"{Name}: " + ex.Message, AuditEventType.Create, ex.InnerException), facilityId);
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
                        _deadLetterExceptionHandler.HandleException(consumeResult,
                            new DeadLetterException($"{Name}: " + ex.Message, AuditEventType.Query, ex.InnerException), facilityId);
                    }
                    finally
                    {
                        if (consumeResult != null)
                        {
                            consumer.Commit(consumeResult);
                        }
                        else
                        {
                            consumer.Commit();
                        }
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
