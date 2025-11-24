using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;
using System.Text;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Report.Listeners
{
    public class PatientListsAcquiredListener : BackgroundService
    {
        private readonly ILogger<PatientListsAcquiredListener> _logger;
        private readonly IKafkaConsumerFactory<string, PatientListMessage> _kafkaConsumerFactory;
        private readonly ITransientExceptionHandler<string, PatientListMessage> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<string, PatientListMessage> _deadLetterExceptionHandler;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ISubmissionEntryManager _submissionEntryManager;
        private string Name => this.GetType().Name;

        public PatientListsAcquiredListener(
            ILogger<PatientListsAcquiredListener> logger, 
            IKafkaConsumerFactory<string, PatientListMessage> kafkaConsumerFactory,
            ISubmissionEntryManager submissionEntryManager,
            ITransientExceptionHandler<string, PatientListMessage> transientExceptionHandler,
            IDeadLetterExceptionHandler<string, PatientListMessage> deadLetterExceptionHandler, 
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));
            _serviceScopeFactory = serviceScopeFactory;

            _submissionEntryManager = submissionEntryManager;

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = KafkaTopic.PatientListsAcquiredRetry.GetStringValue();

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.PatientListsAcquired) + "-Error";
        }

        protected override System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async void StartConsumerLoop(CancellationToken cancellationToken)
        {
            var consumerConfig = new ConsumerConfig()
            {
                GroupId = ReportConstants.ServiceName,
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);

            try
            {
                consumer.Subscribe(nameof(KafkaTopic.PatientListsAcquired));

                _logger.LogInformation("Started PatientListsAcquired consumer for topic '{Topic}' at {StartTime}", nameof(KafkaTopic.PatientListsAcquired), DateTime.UtcNow);

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

                            try
                            {
                                var scope = _serviceScopeFactory.CreateScope();
                                var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

                                var key = result.Message.Key;
                                var value = result.Message.Value.PatientLists;
                                facilityId = key;

                                if (string.IsNullOrWhiteSpace(key) || value == null || value.Any(x => x.PatientIds == null))
                                {
                                    throw new DeadLetterException("Invalid Patient Id's Acquired Event");
                                }

                                var scheduledReports =
                                    await database.ReportScheduledRepository.FindAsync(x => x.FacilityId == key && x.EndOfReportPeriodJobHasRun == false, cancellationToken);

                                if (!scheduledReports?.Any() ?? false)
                                {
                                    throw new TransientException(
                                        $"{Name}: No Scheduled Reports found for facilityId: {key}");
                                }

                                foreach (var scheduledReport in scheduledReports)
                                {
                                    foreach (var reportType in scheduledReport.ReportTypes)
                                    {
                                        foreach (var patientListItem in value)
                                        {
                                            foreach (var pId in patientListItem.PatientIds)
                                            {
                                                var patientId = pId.Split('/').Last();

                                                var entry = await _submissionEntryManager.SingleOrDefaultAsync(e =>
                                                           e.ReportScheduleId == scheduledReport.Id
                                                           && e.PatientId == patientId
                                                           && e.ReportType == reportType, consumeCancellationToken);

                                                if (entry == null)
                                                {
                                                    await _submissionEntryManager.AddAsync(new MeasureReportSubmissionEntryModel()
                                                    {
                                                        PatientId = patientId,
                                                        Status = PatientSubmissionStatus.PendingEvaluation,
                                                        ReportScheduleId = scheduledReport.Id,
                                                        FacilityId = scheduledReport.FacilityId,
                                                        ReportType = reportType,
                                                        CreateDate = DateTime.UtcNow,
                                                    });
                                                }
                                                else
                                                {
                                                    entry.Status = PatientSubmissionStatus.PendingEvaluation;
                                                    await _submissionEntryManager.UpdateAsync(entry);
                                                } 
                                            }
                                        }
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
                                _deadLetterExceptionHandler.HandleException(result, new DeadLetterException("Report - PatientListsAcquired Exception thrown: " + ex.Message), facilityId);
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
                        _logger.LogError(ex, "Error encountered in PatientListsAcquiredListener");
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
