using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Application;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using ReportingStatus = LantanaGroup.Link.Report.Domain.Enums.ReportingStatus;
using SubmissionStatus = LantanaGroup.Link.Report.Domain.Enums.SubmissionStatus;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text;

namespace LantanaGroup.Link.Report.Listeners;

public class PayloadSubmittedListener(
    IKafkaConsumerFactory<string, PayloadSubmittedValue> kafkaConsumerFactory,
    ITransientExceptionHandler<PayloadSubmittedListener, string, PayloadSubmittedValue> transientExceptionHandler,
    IDeadLetterExceptionHandler<PayloadSubmittedListener, string, PayloadSubmittedValue> deadLetterExceptionHandler,
    ILogger<PayloadSubmittedListener> logger,
    IServiceScopeFactory serviceScopeFactory,
    ServiceInformation serviceInformation,
    IExceptionLogger<PayloadSubmittedListener> exceptionLogger)
    : BackgroundService
{
    private string Name => this.GetType().Name;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
    }

    private async Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        deadLetterExceptionHandler.Topic = KafkaTopic.PayloadSubmitted.GetStringValue() + "-Error";
        transientExceptionHandler.Topic = KafkaTopic.PayloadSubmittedRetry.GetStringValue();

        var config = new ConsumerConfig()
        {
            GroupId = serviceInformation.ServiceConfigName,
            EnableAutoCommit = false
        };

        var assignmentTracker = new KafkaAssignmentTracker();
        using var consumer = kafkaConsumerFactory.CreateConsumer(config, assignmentTracker: assignmentTracker);
        try
        {
            // Redrives are published back onto this topic with the report key. A separate
            // redrive topic would be assigned on its own and could submit the manifest twice.
            consumer.Subscribe(nameof(KafkaTopic.PayloadSubmitted));
            logger.LogInformation("{Name}: Started report submitted consumer for topic '{Topic}' at {StartTime}", Name, nameof(KafkaTopic.PayloadSubmitted), DateTime.UtcNow);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                    {
                        var accounted = false;
                        try
                        {
                            accounted = await ProcessMessageAsync(result, consumeCancellationToken);
                            if (!accounted)
                            {
                                await DeadLetterCommit.RewindAsync(consumer, result, logger, consumeCancellationToken);
                            }
                        }
                        catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        finally
                        {
                            if (accounted && result != null && !consumeCancellationToken.IsCancellationRequested)
                            {
                                assignmentTracker.MarkProcessed(result);
                                consumer.SafeCommit(result, logger);
                            }
                        }
                    }, cancellationToken);
                }
                catch (ConsumeException ex)
                {
                    exceptionLogger.Handle(ex, "Error consuming message for topics", LogLevel.Error, null, new { Topics = string.Join(", ", consumer.Subscription) });

                    if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        throw new OperationCanceledException(ex.Error.Reason, ex);
                    }

                    if (ex.ConsumerRecord != null)
                    {
                        string facilityId = KafkaHeaderHelper.GetExceptionFacilityId(ex.ConsumerRecord.Message.Headers);
                        deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);
                    }

                    var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                    consumer.SafeCommit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset }, logger);
                }
                catch (Exception ex)
                {
                    exceptionLogger.Handle(ex, "Error encountered in PayloadSubmittedListener", LogLevel.Error);
                }
            }
        }
        catch (OperationCanceledException oce)
        {
            exceptionLogger.Handle(oce, "Operation Canceled", LogLevel.Error);
            consumer.Close();
        }
    }

    public async Task<bool> ProcessMessageAsync(ConsumeResult<string, PayloadSubmittedValue> result, CancellationToken cancellationToken)
    {
        using var metricsMode = MetricsModeScope.Begin(KafkaHeaderHelper.IsPerformanceMode(result.Message?.Headers));
        var facilityId = KafkaIdentity.Facility(result.Message.Value?.FacilityId, result.Message.Key) ?? string.Empty;
        var reportScheduleId = KafkaIdentity.ReportSchedule(result.Message.Value?.ReportScheduleId, result.Message.Key);
        if (!result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
        {
            throw new DeadLetterException($"{Name}: Received message without correlation ID (ReportId = {reportScheduleId}, FacilityId = {facilityId}).");
        }

        var correlationId = Encoding.UTF8.GetString(headerValue);

        using var scope = serviceScopeFactory.CreateScope();
        var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        var reportManifestProducer = scope.ServiceProvider.GetRequiredService<ReportManifestProducer>();

        try
        {
            if (reportScheduleId is null)
            {
                throw new DeadLetterException($"{Name}: Report schedule id is missing from the message value.");
            }

            var reportTrackingId = reportScheduleId.Value;
            var patientId = KafkaIdentity.Patient(result.Message.Value?.PatientId, result.Message.Key);
            if (await PipelineAbortSkip.ShouldSkipAsync(
                    scope.ServiceProvider, logger, Name, facilityId, reportTrackingId.ToString(), cancellationToken))
                return true;

            var reportSchedule = (await reportScheduledManager.FindAsync(x => x.Id == reportTrackingId, cancellationToken)).Single();

            logger.LogDebug("Consuming PayloadSubmitted (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", facilityId, patientId, reportTrackingId);

            if (result.Message.Value.PayloadType == PayloadType.MeasureReportSubmissionEntry)
            {
                // Submission off: ValidationComplete already stored NotSubmitted and sent this
                // event only so the manifest check stays on the report key. Do not mark the
                // entry submitted. Nothing was copied to the external container.
                if (reportSchedule.EnableSubmission)
                {
                    var reportEntry = await database.ReportEntryRepository.FirstAsync(e => e.PatientId == patientId && e.ReportScheduleId == reportTrackingId, cancellationToken);

                    reportEntry.SubmissionStatus = SubmissionStatus.Submitted;
                    reportEntry.SubmitReportDateTime = DateTime.UtcNow;
                    reportEntry.ModifyDate = DateTime.UtcNow;
                    database.ReportEntryRepository.Update(reportEntry);
                    await database.SaveChangesAsync(cancellationToken);
                }

                await reportManifestProducer.Produce(reportSchedule, correlationId, cancellationToken);
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
                await reportScheduledManager.UpdateAsync(reportSchedule, cancellationToken);
            }

            return true;
        }
        catch (DeadLetterException ex)
        {
            return deadLetterExceptionHandler.HandleException(result, ex, facilityId);
        }
        catch (TransientException ex)
        {
            transientExceptionHandler.HandleException(result, ex, facilityId);
            return true;
        }
        catch (TimeoutException ex)
        {
            var exceptionMessage = $"Timeout exception encountered on {DateTime.UtcNow} for topics: [PayloadSubmitted] at offset: {result.TopicPartitionOffset}";
            var transientException = new TransientException(exceptionMessage, ex);
            transientExceptionHandler.HandleException(result, transientException, facilityId);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            transientExceptionHandler.HandleException(result, ex, facilityId);
            return true;
        }
    }
}