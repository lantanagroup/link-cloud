using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
//using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text;

namespace LantanaGroup.Link.Report.Listeners;

public class PayloadSubmittedListener(
    IKafkaConsumerFactory<PayloadSubmittedKey, PayloadSubmittedValue> kafkaConsumerFactory,
    ITransientExceptionHandler<PayloadSubmittedKey, PayloadSubmittedValue> transientExceptionHandler,
    IDeadLetterExceptionHandler<PayloadSubmittedKey, PayloadSubmittedValue> deadLetterExceptionHandler,
    ILogger<PayloadSubmittedListener> logger,
    IServiceScopeFactory serviceScopeFactory,
    ServiceInformation serviceInformation)
    : BackgroundService
{
    private string Name => this.GetType().Name;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
    }

    private async Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig()
        {
            GroupId = serviceInformation.ServiceConfigName,
            EnableAutoCommit = false
        };

        using var consumer = kafkaConsumerFactory.CreateConsumer(config);
        try
        {
            consumer.Subscribe(nameof(KafkaTopic.PayloadSubmitted));
            logger.LogInformation("Started report submitted consumer for topic '{Topic}' at {StartTime}", nameof(KafkaTopic.PayloadSubmitted), DateTime.UtcNow);

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

                        if (!result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
                        {
                            throw new DeadLetterException($"{Name}: Received message without correlation ID (ReportId = {result.Message.Key.ReportScheduleId}, FacilityId = {result.Message.Key.FacilityId}).");
                        }

                        var correlationId = Encoding.UTF8.GetString(headerValue);

                        var scope = serviceScopeFactory.CreateScope();
                        var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
                        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
                        var reportManifestProducer = scope.ServiceProvider.GetRequiredService<ReportManifestProducer>();

                        var facilityId = result.Message.Key.FacilityId;
                        
                        try
                        {
                            var reportTrackingId = result.Message.Key.ReportScheduleId;
                            var reportSchedule = (await reportScheduledManager.FindAsync(x => x.Id == reportTrackingId, consumeCancellationToken)).Single();

                            logger.LogDebug("Consuming Event (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", facilityId, result.Message.Value.PatientId, reportTrackingId);

                            if (result.Message.Value.PayloadType == PayloadType.MeasureReportSubmissionEntry)
                            {
                                var reportEntry = await database.ReportEntryRepository.FirstAsync(e => e.PatientId == result.Message.Value.PatientId && e.ReportScheduleId == result.Message.Key.ReportScheduleId);

                                reportEntry.SubmissionStatus = SubmissionStatus.Submitted;
                                reportEntry.ModifyDate = DateTime.UtcNow;
                                database.ReportEntryRepository.Update(reportEntry);
                                await database.SaveChangesAsync();

                                //Will build and produce the manifest if this is the last consumed patient payload
                                await reportManifestProducer.Produce(reportSchedule, correlationId);
                            }
                            else if (result.Message.Value.PayloadType == PayloadType.ReportSchedule)
                            {
                                if (reportSchedule == null)
                                {
                                    throw new DeadLetterException($"{Name}: Report schedule {reportTrackingId} not found");
                                }

                                reportSchedule.Status = ScheduleStatus.Submitted;
                                reportSchedule.SubmitReportDateTime = DateTime.UtcNow;
                                reportSchedule.ModifyDate = DateTime.UtcNow;
                                await reportScheduledManager.UpdateAsync(reportSchedule, consumeCancellationToken);
                            }
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
                    logger.LogError(ex, "Error consuming message for topics: [{Topics}] at {Timestamp}",
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
            logger.LogError(oce, "Operation Canceled: {Message}", oce.Message);
            consumer.Close();
        }
    }
}