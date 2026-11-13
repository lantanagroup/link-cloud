namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

/// <summary>
/// Resolves identifiers from the message value, then from the legacy key.
/// TODO: remove the legacy key fallback after one release, once every producer writes the identifiers on the value.
/// </summary>
public static class KafkaIdentity
{
    public static string? Facility(string? valueFacilityId, string? key)
    {
        if (!string.IsNullOrEmpty(valueFacilityId))
        {
            return valueFacilityId;
        }

        if (LinkMessageKey.TryDeserialize(key, out var messageKey) && messageKey != null)
        {
            return messageKey.FacilityId;
        }

        // TODO: remove legacy key fallback after one release once every producer writes the identifiers on the value.
        return KafkaKeyLegacy.TryReadFacility(key, out var legacy) ? legacy : null;
    }

    public static string? Patient(string? valuePatientId, string? key)
    {
        if (!string.IsNullOrEmpty(valuePatientId))
        {
            return valuePatientId;
        }

        if (LinkMessageKey.TryDeserialize(key, out var messageKey) && messageKey != null && !string.IsNullOrEmpty(messageKey.PatientId))
        {
            return messageKey.PatientId;
        }

        // TODO: remove legacy key fallback after one release once every producer writes the identifiers on the value.
        return KafkaKeyLegacy.TryReadPatient(key, out var legacy) ? legacy : null;
    }

    public static Guid? ReportSchedule(Guid? valueReportScheduleId, string? key)
    {
        if (valueReportScheduleId.HasValue && valueReportScheduleId.Value != Guid.Empty)
        {
            return valueReportScheduleId;
        }

        return KafkaKeyLegacy.TryReadReportScheduleId(key, out var legacy) ? legacy : null;
    }

    public static string RequireFacility(string? valueFacilityId, string? key)
    {
        var facilityId = Facility(valueFacilityId, key);
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new InvalidOperationException("Facility id is missing from the message value.");
        }

        return facilityId;
    }

    public static string RequirePatient(string? valuePatientId, string? key)
    {
        var patientId = Patient(valuePatientId, key);
        if (string.IsNullOrWhiteSpace(patientId))
        {
            throw new InvalidOperationException("Patient id is missing from the message value.");
        }

        return patientId;
    }
}
