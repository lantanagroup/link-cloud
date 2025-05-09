using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.Report.KafkaProducers;

public class ReportScheduledProducer(
    IProducer<string, ReportScheduledValue> reportScheduledProducer,
    ILogger<ReportScheduledProducer> logger)
{
    private readonly ILogger<ReportScheduledProducer> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    ///     Produces a report scheduled event to Kafka
    /// </summary>
    /// <param name="facilityId">The facility identifier</param>
    /// <param name="startDate">The start date for the report period</param>
    /// <param name="endDate">The end date for the report period</param>
    /// <param name="reportTypes">List of report types to generate</param>
    /// <param name="frequency">The frequency of the report</param>
    /// <returns>True if the message was successfully delivered to Kafka</returns>
    public async Task<bool> Produce(
        string reportTrackingId,
        string facilityId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        List<string> reportTypes,
        Frequency frequency)
    {
        try
        {
            // Create the value object
            var reportScheduledValue = new ReportScheduledValue
            {
                ReportTrackingId = reportTrackingId,
                StartDate = startDate,
                EndDate = endDate,
                ReportTypes = reportTypes,
                Frequency = frequency
            };

            // Send the message
            var deliveryResult = await reportScheduledProducer.ProduceAsync(
                nameof(KafkaTopic.ReportScheduled),
                new Message<string, ReportScheduledValue>
                {
                    Key = facilityId,
                    Value = reportScheduledValue
                });

            reportScheduledProducer.Flush();

            if (deliveryResult.Status == PersistenceStatus.Persisted)
            {
                _logger.LogDebug(
                    "Report scheduled event successfully produced. Report tracking ID: {ReportTrackingId}, Facility ID: {FacilityId}",
                    reportTrackingId.SanitizeAndRemove(), facilityId.SanitizeAndRemove());
                return true;
            }

            _logger.LogWarning(
                "Report scheduled event not persisted properly. Status: {Status}, Report tracking ID: {ReportTrackingId}",
                deliveryResult.Status, reportTrackingId.SanitizeAndRemove());
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error producing report scheduled event for facility {FacilityId}", facilityId.SanitizeAndRemove());
            return false;
        }
    }
}