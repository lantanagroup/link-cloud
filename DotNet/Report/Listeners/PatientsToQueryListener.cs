using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Commands;
using LantanaGroup.Link.Report.Application.MeasureReportSchedule.Queries;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using MediatR;

namespace LantanaGroup.Link.Report.Listeners
{
    public class PatientsToQueryListener : BackgroundService
    {
        private readonly ILogger<PatientsToQueryListener> _logger;
        private readonly IKafkaConsumerFactory<string, PatientsToQueryValue> _kafkaConsumerFactory;
        private readonly IMediator _mediator;

        private readonly ITransientExceptionHandler<string, PatientsToQueryValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, PatientsToQueryValue> _deadLetterExceptionHandler;

        private string Name => this.GetType().Name;

        public PatientsToQueryListener(ILogger<PatientsToQueryListener> logger, IKafkaConsumerFactory<string, PatientsToQueryValue> kafkaConsumerFactory,
            IMediator mediator,
            ITransientExceptionHandler<string, PatientsToQueryValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<string, PatientsToQueryValue> deadLetterExceptionHandler)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _mediator = mediator ?? throw new ArgumentException(nameof(mediator));

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(_transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(_deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = nameof(KafkaTopic.PatientsToQuery) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.PatientsToQuery) + "-Error";
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
                consumer.Subscribe(nameof(KafkaTopic.PatientsToQuery));
                _logger.LogInformation($"Started patients to query consumer for topic '{nameof(KafkaTopic.PatientsToQuery)}' at {DateTime.UtcNow}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    ConsumeResult<string, PatientsToQueryValue>? consumeResult = null;
                    string facilityId = string.Empty;
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, cancellationToken) =>
                        {
                            consumeResult = result;

                            if (consumeResult == null)
                            {
                                throw new DeadLetterException(
                                    $"{Name}: consumeResult is null", AuditEventType.Create);
                            }

                            var key = consumeResult.Message.Key;
                            var value = consumeResult.Message.Value;
                            facilityId = key;

                            if (string.IsNullOrWhiteSpace(key))
                            {
                                throw new DeadLetterException($"{Name}: key value is null or empty", AuditEventType.Create);
                            }

                            if (value == null || value.PatientIds == null)
                            {
                                throw new DeadLetterException(
                                    $"{Name}: consumeResult.Value.PatientIds is null", AuditEventType.Create);
                            }

                            var scheduledReports = await _mediator.Send(new FindMeasureReportScheduleForFacilityQuery() { FacilityId = key }, cancellationToken);

                            if (!scheduledReports?.Any() ?? false)
                            {
                                throw new DeadLetterException($"{Name}: No Scheduled Reports found for facilityId: {key}", AuditEventType.Query);
                            }

                            foreach (var scheduledReport in scheduledReports.Where(sr => !sr.PatientsToQueryDataRequested.GetValueOrDefault()))
                            {
                                scheduledReport.PatientsToQuery = value.PatientIds;

                                await _mediator.Send(new UpdateMeasureReportScheduleCommand()
                                {
                                    ReportSchedule = scheduledReport

                                }, cancellationToken);
                            }

                        }, cancellationToken);
                        
                    }
                    catch (ConsumeException ex)
                    {
                        _deadLetterExceptionHandler.HandleException(new DeadLetterException($"{Name}: " + ex.Message, AuditEventType.Create, ex.InnerException), facilityId);
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
                        _deadLetterExceptionHandler.HandleException(ex, facilityId, AuditEventType.Create);
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
                _logger.LogError(oce, $"Operation Canceled: {oce.Message}");
                consumer.Close();
                consumer.Dispose();
            }

        }

    }
}
