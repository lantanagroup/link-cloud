using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Commands;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using System.Text;
using LantanaGroup.Link.Report.Application.Error.Exceptions;
using LantanaGroup.Link.Report.Application.Error.Interfaces;

namespace LantanaGroup.Link.Report.Listeners
{
    public class ReportSubmittedListener : BackgroundService
    {

        private readonly ILogger<ReportSubmittedListener> _logger;
        private readonly IKafkaConsumerFactory<ReportSubmittedKey, ReportSubmittedValue> _kafkaConsumerFactory;
        //Doesn't actually generate a SubmissionReport but prevents us from having to declare a new factory type in the initialization
        //for a ProducerFactory that's only used for an AuditEvent
        private readonly IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue> _kafkaProducerFactory;
        private readonly IMediator _mediator;

        private readonly IReportTransientExceptionHandler<ReportSubmittedKey, ReportSubmittedValue> _reportTransientExceptionHandler;
        private readonly IReportExceptionHandler<ReportSubmittedKey, ReportSubmittedValue> _reportExceptionHandler;

        public ReportSubmittedListener(ILogger<ReportSubmittedListener> logger, IKafkaConsumerFactory<ReportSubmittedKey, ReportSubmittedValue> kafkaConsumerFactory,
            IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue> kafkaProducerFactory, IMediator mediator,
            IReportTransientExceptionHandler<ReportSubmittedKey, ReportSubmittedValue> reportTransientExceptionHandler,
            IReportExceptionHandler<ReportSubmittedKey, ReportSubmittedValue> reportExceptionHandler)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentException(nameof(kafkaProducerFactory));
            _mediator = mediator ?? throw new ArgumentException(nameof(mediator));

            _reportTransientExceptionHandler = reportTransientExceptionHandler ??
                                               throw new ArgumentException(nameof(reportExceptionHandler));

            _reportExceptionHandler = reportExceptionHandler ??
                                      throw new ArgumentException(nameof(reportExceptionHandler));
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }


        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig()
            {
                GroupId = "ReportSubmittedEvent",
                EnableAutoCommit = false
            };

            using (var consumer = _kafkaConsumerFactory.CreateConsumer(config))
            {
                try
                {
                    consumer.Subscribe(nameof(KafkaTopic.ReportSubmitted));
                    _logger.LogInformation($"Started report submitted consumer for topic '{nameof(KafkaTopic.ReportSubmitted)}' at {DateTime.UtcNow}");

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var consumeResult = new ConsumeResult<ReportSubmittedKey, ReportSubmittedValue>();
                        try
                        {
                            consumeResult = consumer.Consume(cancellationToken);

                            if (consumeResult == null)
                            {
                                throw new TerminatingException(
                                    "ReportSubmittedListener: Result of ConsumeResult<ReportSubmittedKey, ReportSubmittedValue>.Consume is null");
                            }

                            ReportSubmittedKey key = consumeResult.Message.Key;
                            ReportSubmittedValue value = consumeResult.Message.Value;

                            // find existing report schedule
                            MeasureReportScheduleModel schedule = await _mediator.Send(new GetMeasureReportScheduleByBundleIdQuery { ReportBundleId = value.ReportBundleId });

                            if (schedule is null)
                            {
                                throw new TransientException(
                                    $"No report schedule found for submission bundle with ID {value.ReportBundleId}");
                            }

                            // update report schedule with submitted date
                            schedule.SubmittedDate = DateTime.UtcNow;
                            await _mediator.Send(new UpdateMeasureReportScheduleCommand { ReportSchedule = schedule });

                            consumer.Commit(consumeResult);

                            // produce audit message signalling the report service acknowledged the report has been submitted
                            using var producer = _kafkaProducerFactory.CreateAuditEventProducer();
                            try
                            {
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
                            catch (Exception ex)
                            {
                                throw new TerminatingException("ReportSubmittedListener: " + ex.Message, ex.InnerException);
                            }
                        }
                        catch (ConsumeException e)
                        {
                            _logger.LogError($"Consumer error: {e.Error.Reason}");
                            _logger.LogError(e.InnerException?.Message);
                            if (e.Error.IsFatal)
                            {
                                break;
                            }
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
                            _reportExceptionHandler.HandleException(consumeResult, new TerminatingException("ReportSubmittedListener: " + ex.Message, ex.InnerException));
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
}
