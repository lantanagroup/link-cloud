using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Commands;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Services;
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
        private readonly ISchedulerFactory _schedulerFactory;


        public ReportScheduledListener(ILogger<ReportScheduledListener> logger, IKafkaConsumerFactory<MeasureReportScheduledKey, MeasureReportScheduledValue> kafkaConsumerFactory,
            IMediator mediator, ISchedulerFactory schedulerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _schedulerFactory = schedulerFactory ?? throw new ArgumentException(nameof(schedulerFactory));
            _mediator = mediator ?? throw new ArgumentException(nameof(mediator));
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

            using (var _reportScheduledConsumer = _kafkaConsumerFactory.CreateConsumer(config))
            {
                try
                {
                    _reportScheduledConsumer.Subscribe(nameof(KafkaTopic.ReportScheduled));
                    _logger.LogInformation($"Started report scheduled consumer for topic '{nameof(KafkaTopic.ReportScheduled)}' at {DateTime.UtcNow}");

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            var consumeResult = _reportScheduledConsumer.Consume(cancellationToken);

                            if (consumeResult != null)
                            {
                                MeasureReportScheduledKey key = consumeResult.Message.Key;
                                MeasureReportScheduledValue value = consumeResult.Message.Value;

                                var startDateOffset = DateTimeOffset.Parse(value.Parameters.Single(x => x.Key.ToLower() == "startdate").Value);
                                var endDateOffset = DateTimeOffset.Parse(value.Parameters.Single(x => x.Key.ToLower() == "enddate").Value);
                                var startDate = startDateOffset.UtcDateTime;
                                var endDate = endDateOffset.UtcDateTime;

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

                                var scheduleTrigger = cronSchedule.CronExpressionString;

                                // create or update the consumed report schedule
                                var existing = await _mediator.Send(new FindMeasureReportScheduleForReportTypeQuery() { FacilityId = key.FacilityId, ReportStartDate = startDate, ReportEndDate = endDate, ReportType = key.ReportType}, cancellationToken);
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
                                        await MeasureReportScheduleService.RescheduleJob(existing, await _schedulerFactory.GetScheduler(cancellationToken));
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

                                    await MeasureReportScheduleService.CreateJobAndTrigger(reportSchedule, await _schedulerFactory.GetScheduler(cancellationToken));
                                }

                                _reportScheduledConsumer.Commit(consumeResult);
                            }
                        }
                        catch (ConsumeException e)
                        {
                            _logger.LogError($"Consumer error: {e.Error.Reason}");
                            if (e.Error.IsFatal)
                            {
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"An exception occurred in the Report Scheduled Consumer service: {ex.Message}");
                        }
                    }
                }
                catch (OperationCanceledException oce)
                {
                    _logger.LogError($"Operation Canceled: {oce.Message}", oce);
                    _reportScheduledConsumer.Close();
                    _reportScheduledConsumer.Dispose();
                }
            }

        }

    }
}
