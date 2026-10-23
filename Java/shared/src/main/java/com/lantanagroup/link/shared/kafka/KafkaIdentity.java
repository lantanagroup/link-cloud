package com.lantanagroup.link.shared.kafka;

import java.util.UUID;

/**
 * Resolves identifiers from the message value, then from the legacy key.
 */
public final class KafkaIdentity {

    private static final UUID NIL_UUID = new UUID(0L, 0L);

    private KafkaIdentity() {
    }

    public static String facility(String valueFacilityId, String key) {
        if (!isBlank(valueFacilityId)) {
            return valueFacilityId;
        }
        return KafkaKeyLegacy.tryReadFacility(key);
    }

    public static String patient(String valuePatientId, String key) {
        if (!isBlank(valuePatientId)) {
            return valuePatientId;
        }
        return KafkaKeyLegacy.tryReadPatient(key);
    }

    public static UUID reportSchedule(UUID valueReportScheduleId, String key) {
        if (valueReportScheduleId != null && !NIL_UUID.equals(valueReportScheduleId)) {
            return valueReportScheduleId;
        }
        return KafkaKeyLegacy.tryReadReportScheduleId(key);
    }

    private static boolean isBlank(String value) {
        return value == null || value.isBlank();
    }
}
