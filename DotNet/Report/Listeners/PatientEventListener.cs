using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;
using System.Text;

namespace LantanaGroup.Link.Report.Listeners
{
    public class PatientEventListener : BackgroundService
    {
        private readonly ILogger<PatientEventListener> _logger;
        private readonly IKafkaConsumerFactory<string, PatientEventValue> _kafkaConsumerFactory;
        private readonly ITransientExceptionHandler<string, PatientEventValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, PatientEventValue> _deadLetterExceptionHandler;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ServiceInformation _serviceInformation;

        private string Name => this.GetType().Name;

        public PatientEventListener(
            ILogger<PatientEventListener> logger,
            IKafkaConsumerFactory<string, PatientEventValue> kafkaConsumerFactory,
            ITransientExceptionHandler<string, PatientEventValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<string, PatientEventValue> deadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory, ServiceInformation serviceInformation)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _serviceScopeFactory = serviceScopeFactory;
            _serviceInformation = serviceInformation;

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.Topic = KafkaTopic.PatientEventRetry.GetStringValue();
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.PatientEvent) + "-Error";
        }

        protected override System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return System.Threading.Tasks.Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var consumerConfig = new ConsumerConfig()
            {
                GroupId = _serviceInformation.ServiceConfigName,
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);

            try
            {
                consumer.Subscribe(nameof(KafkaTopic.PatientEvent));

                _logger.LogInformation("Started PatientEvent consumer for topic '{Topic}' at {StartTime}", nameof(KafkaTopic.PatientEvent), DateTime.UtcNow);

                while (!cancellationToken.IsCancellationRequested)
                {
                    string facilityId = string.Empty;

                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            if (result == null)
                            {
                                consumer.Commit();
                                return;
                            }

                            var reportEntryManager = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IReportEntryManager>();

                            try
                            {
                                using var scope = _serviceScopeFactory.CreateScope();
                                var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

                                var key = result.Message.Key;
                                var value = result.Message.Value;
                                facilityId = key;

                                if (string.IsNullOrWhiteSpace(key) || value == null)
                                {
                                    throw new DeadLetterException("Invalid Patient Event");
                                }

                                if (value.EventType != PatientEvents.Admit.ToString())
                                {
                                    _logger.LogInformation("Patient {PatientId} has event type of {EventType}. Ignoring.", HtmlInputSanitizer.Sanitize(value.PatientId), HtmlInputSanitizer.Sanitize(value.EventType));
                                    consumer.Commit(result);
                                    return;
                                }

                                _logger.LogInformation("Consuming Admit PatientEvent (FacilityId: {facilityId}, PatientId: {PatientId})", HtmlInputSanitizer.Sanitize(facilityId), HtmlInputSanitizer.Sanitize(value.PatientId));

                                var scheduledReports = await database.ReportScheduledRepository.FindAsync(x => x.FacilityId == key && x.EndOfReportPeriodJobHasRun == false, cancellationToken);

                                if (!scheduledReports?.Any() ?? false)
                                {
                                    throw new TransientException($"{Name}: No Scheduled Reports found for facilityId: {key}");
                                }

                                foreach (var scheduledReport in scheduledReports)
                                {
                                    var entry = await reportEntryManager.SingleOrDefaultAsync(e => e.ReportScheduleId == scheduledReport.Id && e.PatientId == value.PatientId, consumeCancellationToken);

                                    if (entry == null)
                                    {
                                        entry = new ReportEntry()
                                        {
                                            PatientId = value.PatientId,
                                            FacilityId = scheduledReport.FacilityId,
                                            CreateDate = DateTime.Now,
                                            ReportingStatus = ReportingStatus.PatientIdentified,
                                            ReportScheduleId = scheduledReport.Id
                                        };
                                        await reportEntryManager.AddAsync(entry, cancellationToken);
                                    }

                                    foreach (var reportType in scheduledReport.ReportTypes)
                                    {
                                        var measureReportEntry = entry.MeasureReportList.Where(x => x.ReportType == reportType).FirstOrDefault();

                                        if (measureReportEntry != null)
                                        {
                                            continue;
                                        }

                                        entry.MeasureReportList.Add(new EvaluatedMeasureReport()
                                        {
                                            ReportType = reportType
                                        });

                                        await reportEntryManager.UpdateAsync(entry);
                                    }
                                }
                            }
                            catch (DeadLetterException ex)
                            {
                                _deadLetterExceptionHandler.HandleException(result, ex, facilityId);
                            }
                            catch (TransientException ex)
                            {
                                _transientExceptionHandler.HandleException(result, ex, facilityId);
                            }
                            catch (Exception ex)
                            {
                                _deadLetterExceptionHandler.HandleException(result, new DeadLetterException("Report - PatientEvent Exception thrown: " + ex.Message), facilityId);
                            }
                            finally
                            {
                                consumer.Commit(result);
                            }
                        }, cancellationToken);
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming message for topics: [{Topics}] at {Timestamp}", string.Join(", ", consumer.Subscription), DateTime.UtcNow);

                        if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                        {
                            throw new OperationCanceledException(ex.Error.Reason, ex);
                        }

                        facilityId = GetFacilityIdFromHeader(ex.ConsumerRecord.Message.Headers);

                        _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);

                        var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                        consumer.Commit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error encountered in PatientEventListener");
                        consumer.Commit();
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError(oce, "Operation Canceled: {Message}", oce.Message);
                consumer.Close();
                consumer.Dispose();
            }
        }

        private static string GetFacilityIdFromHeader(Headers headers)
        {
            string facilityId = string.Empty;

            if (headers.TryGetLastBytes(KafkaConstants.HeaderConstants.ExceptionFacilityId, out var facilityIdBytes))
            {
                facilityId = Encoding.UTF8.GetString(facilityIdBytes);
            }

            return facilityId;
        }
    }
}
