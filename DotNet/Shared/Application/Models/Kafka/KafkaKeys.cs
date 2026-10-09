namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

/// <summary>
/// Partition keys. Producers send <see cref="LinkMessageKey.Serialize"/> bytes.
/// The key is not the source of business data when the value carries the same ids.
/// </summary>
public static class KafkaKeys
{
    public static string ForPatient(string? facilityId, string? patientId)
    {
        if (string.IsNullOrEmpty(facilityId))
        {
            throw new ArgumentException("Facility id is required.", nameof(facilityId));
        }

        if (string.IsNullOrEmpty(patientId))
        {
            throw new ArgumentException("Patient id is required.", nameof(patientId));
        }

        return new LinkMessageKey(facilityId, patientId).Serialize();
    }

    public static string ForFacility(string? facilityId)
    {
        if (string.IsNullOrEmpty(facilityId))
        {
            throw new ArgumentException("Facility id is required.", nameof(facilityId));
        }

        return new LinkMessageKey(facilityId, null).Serialize();
    }

    /// <summary>
    /// One key for every completion of a report. PayloadSubmitted and the manifest
    /// SubmitPayload use this so the last-patient check runs on one consumer.
    /// </summary>
    public static string ForReport(string? facilityId, Guid reportScheduleId)
    {
        if (string.IsNullOrEmpty(facilityId))
        {
            throw new ArgumentException("Facility id is required.", nameof(facilityId));
        }

        return LinkMessageKey.SerializeReport(facilityId, reportScheduleId);
    }

    /// <summary>
    /// Key for a message that has neither a facility nor a patient, such as a service health check.
    /// </summary>
    public static string ForService(string? serviceName)
    {
        if (string.IsNullOrEmpty(serviceName))
        {
            throw new ArgumentException("Service name is required.", nameof(serviceName));
        }

        LinkMessageKey.RejectUnpairedSurrogates(serviceName);
        return serviceName;
    }

    /// <summary>
    /// Patient messages use <see cref="ForPatient"/>. Facility-scoped messages use <see cref="ForFacility"/>.
    /// A message with neither uses <see cref="ForService"/> so the key is never empty.
    /// </summary>
    public static string ForAudit(string? facilityId, string? patientId, string? serviceName)
    {
        if (!string.IsNullOrEmpty(patientId))
        {
            return ForPatient(facilityId, patientId);
        }

        if (!string.IsNullOrEmpty(facilityId))
        {
            return ForFacility(facilityId);
        }

        return ForService(serviceName);
    }
}
