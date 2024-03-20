using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Commands;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using System.Text;
using LantanaGroup.Link.Shared.Application.Error.Handlers;

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

        private readonly ITransientExceptionHandler<ReportSubmittedKey, ReportSubmittedValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<ReportSubmittedKey, ReportSubmittedValue> _deadLetterExceptionHandler;

        public ReportSubmittedListener(ILogger<ReportSubmittedListener> logger, IKafkaConsumerFactory<ReportSubmittedKey, ReportSubmittedValue> kafkaConsumerFactory,
            IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue> kafkaProducerFactory, IMediator mediator,
            ITransientExceptionHandler<ReportSubmittedKey, ReportSubmittedValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<ReportSubmittedKey, ReportSubmittedValue> deadLetterExceptionHandler)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentException(nameof(kafkaProducerFactory));
            _mediator = mediator ?? throw new ArgumentException(nameof(mediator));

            _transientExceptionHandler = transientExceptionHandler ??
                                               throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _deadLetterExceptionHandler = deadLetterExceptionHandler ??
                                      throw new ArgumentException(nameof(deadLetterExceptionHandler));

            var t = (TransientExceptionHandler<ReportSubmittedKey, ReportSubmittedValue>)_transientExceptionHandler;
            t.ServiceName = "Report";
            t.Topic = nameof(KafkaTopic.ReportSubmitted) + "-Retry";

            var d = (DeadLetterExceptionHandler<ReportSubmittedKey, ReportSubmittedValue>)_deadLetterExceptionHandler;
            d.ServiceName = "Report";
            d.Topic = nameof(KafkaTopic.ReportSubmitted) + "-Error";
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
                                consumeResult = new ConsumeResult<ReportSubmittedKey, ReportSubmittedValue>();
                                throw new DeadLetterException(
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
                                throw new DeadLetterException("ReportSubmittedListener: " + ex.Message, ex.InnerException);
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
                            _deadLetterExceptionHandler.HandleException(consumeResult, new DeadLetterException("ReportSubmittedListener: " + ex.Message, ex.InnerException));
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
