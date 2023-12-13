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


        public ReportSubmittedListener(ILogger<ReportSubmittedListener> logger, IKafkaConsumerFactory<ReportSubmittedKey, ReportSubmittedValue> kafkaConsumerFactory, 
            IKafkaProducerFactory<SubmissionReportKey, SubmissionReportValue> kafkaProducerFactory, IMediator mediator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentException(nameof(kafkaProducerFactory));
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
                GroupId = "ReportSubmittedEvent",
                EnableAutoCommit = false
            };

            using (var _reportSubmittedConsumer = _kafkaConsumerFactory.CreateConsumer(config))
            {
                try
                {
                    _reportSubmittedConsumer.Subscribe(nameof(KafkaTopic.ReportSubmitted));
                    _logger.LogInformation($"Started report submitted consumer for topic '{nameof(KafkaTopic.ReportSubmitted)}' at {DateTime.UtcNow}");

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            var consumeResult = _reportSubmittedConsumer.Consume(cancellationToken);

                            if (consumeResult != null)
                            {
                                ReportSubmittedKey key = consumeResult.Message.Key;
                                ReportSubmittedValue value = consumeResult.Message.Value;


                                // find existing report schedule
                                MeasureReportScheduleModel schedule = await _mediator.Send(new GetMeasureReportScheduleByBundleIdQuery { ReportBundleId = value.ReportBundleId });
                                if (schedule is null)
                                    throw new Exception($"No report schedule found for submission bundle with ID {value.ReportBundleId}");

                                // update report schedule with submitted date
                                schedule.SubmittedDate = DateTime.UtcNow;
                                await _mediator.Send(new UpdateMeasureReportScheduleCommand { ReportSchedule = schedule });

                                _logger.LogInformation($"Report schedule with ID {schedule.Id} updated.");

                                _reportSubmittedConsumer.Commit(consumeResult);


                                // produce audit message signalling the report service acknowledged the report has been submitted
                                using (var producer = _kafkaProducerFactory.CreateAuditEventProducer())
                                {
                                    try
                                    {
                                        string notes = $"{ReportConstants.ServiceName} has processed the {nameof(KafkaTopic.ReportSubmitted)} event for report bundle with ID {value.ReportBundleId} with report schedule ID {schedule.Id}";
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

                                        producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                                        {
                                            Value = val,
                                            Headers = headers
                                        });
                                        producer.Flush();
                                        _logger.LogInformation(notes);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError($"Failed to generate a {KafkaTopic.AuditableEventOccurred} message", ex);
                                    }
                                }

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
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"An exception occurred in the Measure Evaluated Consumer service: {ex.Message}");
                        }
                    }
                }
                catch (OperationCanceledException oce)
                {
                    _logger.LogError($"Operation Canceled: {oce.Message}", oce);
                    _reportSubmittedConsumer.Close();
                    _reportSubmittedConsumer.Dispose();
                }
            }

        }

    }
}
