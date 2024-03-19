using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Commands;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Shared.Application.Models;

using MediatR;
using Quartz;
using LantanaGroup.Link.Report.Application.Error.Interfaces;
using LantanaGroup.Link.Report.Application.Error.Exceptions;
using static Confluent.Kafka.ConfigPropertyNames;

namespace LantanaGroup.Link.Report.Listeners
{
    public class PatientsToQueryListener : BackgroundService
    {
        private readonly ILogger<PatientsToQueryListener> _logger;
        private readonly IKafkaConsumerFactory<string, PatientsToQueryValue> _kafkaConsumerFactory;
        private readonly IMediator _mediator;

        private readonly IReportTransientExceptionHandler<string, PatientsToQueryValue> _reportTransientExceptionHandler;
        private readonly IReportExceptionHandler<string, PatientsToQueryValue> _reportExceptionHandler;

        public PatientsToQueryListener(ILogger<PatientsToQueryListener> logger, IKafkaConsumerFactory<string, PatientsToQueryValue> kafkaConsumerFactory,
            IMediator mediator,
            IReportTransientExceptionHandler<string, PatientsToQueryValue> reportScheduledTransientExceptionhandler,
            IReportExceptionHandler<string, PatientsToQueryValue> reportScheduledExceptionhandler)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _mediator = mediator ?? throw new ArgumentException(nameof(mediator));

            _reportTransientExceptionHandler = reportScheduledTransientExceptionhandler ?? throw new ArgumentException(nameof(reportScheduledTransientExceptionhandler));
            _reportExceptionHandler = reportScheduledExceptionhandler ?? throw new ArgumentException(nameof(reportScheduledExceptionhandler));
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

            using var consumer = _kafkaConsumerFactory.CreateConsumer(config);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.PatientsToQuery));
                _logger.LogInformation($"Started patients to query consumer for topic '{nameof(KafkaTopic.PatientsToQuery)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var consumeResult = new ConsumeResult<string, PatientsToQueryValue>();
                    try
                    {
                        consumeResult = consumer.Consume(cancellationToken);

                        if (consumeResult != null)
                        {
                            var key = consumeResult.Message.Key;
                            var value = consumeResult.Message.Value;

                            if (string.IsNullOrWhiteSpace(key))
                            {
                                throw new TerminatingException("PatientsToQueryListener: key value is null or empty");
                            }

                            var scheduledReports = await _mediator.Send(new FindMeasureReportScheduleForFacilityQuery() { FacilityId = key }, cancellationToken);
                            foreach (var scheduledReport in scheduledReports.Where(sr => !sr.PatientsToQueryDataRequested.GetValueOrDefault()))
                            {
                                scheduledReport.PatientsToQuery = value.PatientIds;

                                await _mediator.Send(new UpdateMeasureReportScheduleCommand()
                                {
                                    ReportSchedule = scheduledReport

                                }, cancellationToken);
                            }

                            consumer.Commit(consumeResult);
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        consumer.Commit(consumeResult);
                        _reportExceptionHandler.HandleException(consumeResult, new TerminatingException("PatientsToQueryListener: " + ex.Message, ex.InnerException));
                    }
                    catch (TerminatingException ex)
                    {
                        consumer.Commit(consumeResult);
                        _reportExceptionHandler.HandleException(consumeResult, ex);
                    }
                    catch (TransientException ex)
                    {
                        _reportTransientExceptionHandler.HandleException(consumeResult, ex);
                        consumer.Commit(consumeResult);
                    }
                    catch (Exception ex)
                    {
                        consumer.Commit(consumeResult);
                        _reportExceptionHandler.HandleException(consumeResult, new TerminatingException("PatientsToQueryListener: " + ex.Message, ex.InnerException));
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
