using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Commands;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Shared.Application.Models;

using MediatR;
using Quartz;

namespace LantanaGroup.Link.Report.Listeners
{
    public class PatientsToQueryListener : BackgroundService
    {
        private readonly ILogger<PatientsToQueryListener> _logger;
        private readonly IKafkaConsumerFactory<string, PatientsToQueryValue> _kafkaConsumerFactory;
        private readonly IMediator _mediator;

        public PatientsToQueryListener(ILogger<PatientsToQueryListener> logger, IKafkaConsumerFactory<string, PatientsToQueryValue> kafkaConsumerFactory,
            IMediator mediator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
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
                GroupId = "PatientsToQueryEvent",
                EnableAutoCommit = false
            };

            using (var _reportScheduledConsumer = _kafkaConsumerFactory.CreateConsumer(config))
            {
                try
                {
                    _reportScheduledConsumer.Subscribe(nameof(KafkaTopic.PatientsToQuery));
                    _logger.LogInformation($"Started patients to query consumer for topic '{nameof(KafkaTopic.PatientsToQuery)}' at {DateTime.UtcNow}");

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            var consumeResult = _reportScheduledConsumer.Consume(cancellationToken);

                            if (consumeResult != null)
                            {
                                var key = consumeResult.Message.Key;
                                var value = consumeResult.Message.Value;

                                var scheduledReports = await _mediator.Send(new FindMeasureReportScheduleForFacilityQuery() { FacilityId = key }, cancellationToken);
                                foreach (var scheduledReport in scheduledReports.Where(sr => !sr.PatientsToQueryDataRequested.GetValueOrDefault()))
                                {
                                    scheduledReport.PatientsToQuery = value.PatientIds;

                                    await _mediator.Send( new UpdateMeasureReportScheduleCommand()
                                    {
                                        ReportSchedule = scheduledReport 

                                    }, cancellationToken);
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
                            _logger.LogError(ex, $"An exception occurred in the Patients to Query Consumer service: {ex.Message}");
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
