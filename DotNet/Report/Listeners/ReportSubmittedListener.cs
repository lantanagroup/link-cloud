using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.Report.Listeners;

public class ReportSubmittedListener(
    IKafkaConsumerFactory<ReportSubmittedKey, ReportSubmittedValue> kafkaConsumerFactory,
    ITransientExceptionHandler<ReportSubmittedKey, ReportSubmittedValue> transientExceptionHandler,
    IDeadLetterExceptionHandler<ReportSubmittedKey, ReportSubmittedValue> deadLetterExceptionHandler,
    ILogger<ReportSubmittedListener> logger,
    IDatabase database)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
    }

    private async Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig()
        {
            GroupId = ReportConstants.ServiceName,
            EnableAutoCommit = false
        };

        using var consumer = kafkaConsumerFactory.CreateConsumer(config);
        try
        {
            consumer.Subscribe(nameof(KafkaTopic.ReportSubmitted));
            logger.LogInformation(
                $"Started report submitted consumer for topic '{nameof(KafkaTopic.ReportSubmitted)}' at {DateTime.UtcNow}");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                    {
                        if (result == null)
                        {
                            consumer.Commit();
                            return;
                        }
                        
                        var facilityId = result.Message.Key.FacilityId;
                        
                        try
                        {
                            var reportTrackingId = result.Message.Value.ReportTrackingId;
                            var reportSchedule = await database.ReportScheduledRepository
                                .FirstAsync(x => x.Id == reportTrackingId, consumeCancellationToken);

                            if (reportSchedule == null)
                            {
                                logger.LogError($"Report schedule {reportTrackingId} not found");
                                throw new Exception($"Report schedule {reportTrackingId} not found");
                            }

                            logger.LogInformation($"Report submitted for {reportSchedule.FacilityId} at {DateTime.UtcNow}");
                            
                            reportSchedule.SubmitReportDateTime = DateTime.UtcNow;
                            await database.ReportScheduledRepository.UpdateAsync(reportSchedule, consumeCancellationToken);
                        }
                        catch (DeadLetterException ex)
                        {
                            deadLetterExceptionHandler.HandleException(result, ex, facilityId);
                        }
                        catch (TransientException ex)
                        {
                            transientExceptionHandler.HandleException(result, ex, facilityId);
                        }
                        catch (TimeoutException ex)
                        {
                            var exceptionMessage = $"Timeout exception encountered on {DateTime.UtcNow} for topics: [{string.Join(", ", consumer.Subscription)}] at offset: {result.TopicPartitionOffset}";
                            var transientException = new TransientException(exceptionMessage, ex);
                            transientExceptionHandler.HandleException(result, transientException, facilityId);
                        }
                        catch (Exception ex)
                        {
                            transientExceptionHandler.HandleException(result, ex, facilityId);
                        }
                        finally
                        {
                            consumer.Commit(result);
                        }
                    },
                    cancellationToken);
                }
                catch (ConsumeException ex)
                {
                    logger.LogError(ex, "Error consuming message for topics: [{1}] at {2}",
                        string.Join(", ", consumer.Subscription), DateTime.UtcNow);

                    if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        throw new OperationCanceledException(ex.Error.Reason, ex);
                    }

                    if (ex.ConsumerRecord != null)
                    {
                        string facilityId =
                            KafkaHeaderHelper.GetExceptionFacilityId(ex.ConsumerRecord.Message.Headers);
                        deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);
                    }

                    var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                    consumer.Commit(offset == null
                        ? new List<TopicPartitionOffset>()
                        : new List<TopicPartitionOffset> { offset });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error encountered in ReportScheduledListener");
                    consumer.Commit();
                }
            }
        }
        catch (OperationCanceledException oce)
        {
            logger.LogError(oce, $"Operation Canceled: {oce.Message}");
            consumer.Close();
        }
    }
}