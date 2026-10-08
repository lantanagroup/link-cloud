package com.lantanagroup.link.shared.kafka;

/**
 * Partition keys. Producers emit {@link #serialize(String, String)} bytes.
 * The key is not the source of business data when the value carries the same ids.
 */
public final class KafkaKeys {

    private KafkaKeys() {
    }

    public static String forPatient(String facilityId, String patientId) {
        if (isEmpty(facilityId)) {
            throw new IllegalArgumentException("Facility id is required.");
        }
        if (isEmpty(patientId)) {
            throw new IllegalArgumentException("Patient id is required.");
        }
        return serialize(facilityId, patientId);
    }

    public static String forFacility(String facilityId) {
        if (isEmpty(facilityId)) {
            throw new IllegalArgumentException("Facility id is required.");
        }
        return serialize(facilityId, null);
    }

    /**
     * Key for a message that has neither a facility nor a patient, such as a service health check.
     */
    public static String forService(String serviceName) {
        if (isEmpty(serviceName)) {
            throw new IllegalArgumentException("Service name is required.");
        }
        rejectUnpairedSurrogates(serviceName);
        return serviceName;
    }

    /**
     * Patient messages use {@link #forPatient}. Facility-scoped messages use {@link #forFacility}.
     * A message with neither uses {@link #forService} so the key is never empty.
     */
    public static String forAudit(String facilityId, String patientId, String serviceName) {
        if (!isEmpty(patientId)) {
            return forPatient(facilityId, patientId);
        }
        if (!isEmpty(facilityId)) {
            return forFacility(facilityId);
        }
        return forService(serviceName);
    }

    static String serialize(String facilityId, String patientId) {
        String facility = escape(facilityId);
        if (isEmpty(patientId)) {
            return "{\"facilityId\":\"" + facility + "\"}";
        }
        return "{\"facilityId\":\"" + facility + "\",\"patientId\":\"" + escape(patientId) + "\"}";
    }

    private static boolean isEmpty(String value) {
        return value == null || value.isEmpty();
    }

    private static void rejectUnpairedSurrogates(String value) {
        for (int i = 0; i < value.length(); i++) {
            char character = value.charAt(i);
            if (Character.isHighSurrogate(character)) {
                if (i + 1 >= value.length() || !Character.isLowSurrogate(value.charAt(i + 1))) {
                    throw new IllegalArgumentException("Identifier contains an unpaired surrogate.");
                }
                i++;
                continue;
            }
            if (Character.isLowSurrogate(character)) {
                throw new IllegalArgumentException("Identifier contains an unpaired surrogate.");
            }
        }
    }

    private static String escape(String value) {
        rejectUnpairedSurrogates(value);
        StringBuilder builder = new StringBuilder(value.length() + 8);
        for (int i = 0; i < value.length(); i++) {
            char character = value.charAt(i);
            switch (character) {
                case '"':
                    builder.append("\\\"");
                    break;
                case '\\':
                    builder.append("\\\\");
                    break;
                case '\b':
                    builder.append("\\b");
                    break;
                case '\f':
                    builder.append("\\f");
                    break;
                case '\n':
                    builder.append("\\n");
                    break;
                case '\r':
                    builder.append("\\r");
                    break;
                case '\t':
                    builder.append("\\t");
                    break;
                default:
                    if (character < 0x20) {
                        builder.append("\\u");
                        builder.append(String.format("%04x", (int) character));
                    } else {
                        builder.append(character);
                    }
                    break;
            }
        }
        return builder.toString();
    }
}
