package com.lantanagroup.link.shared.audit;

import java.math.BigDecimal;
import java.util.Date;

/**
 * Turns property values into the text recorded in an audit event.
 */
final class AuditValues {
    private AuditValues() {
    }

    /**
     * The text of a value: empty for null, the name of an enum, an ISO-8601 instant for a {@link Date}, and
     * plain (never exponent) notation for decimal numbers. {@code java.time} types use their own ISO-8601
     * {@code toString()}.
     */
    static String text(Object value) {
        if (value == null) {
            return "";
        }
        if (value instanceof Enum<?> e) {
            return e.name();
        }
        // java.sql.Date and java.sql.Time extend java.util.Date but their toInstant() throws
        if (value instanceof java.sql.Date sqlDate) {
            return sqlDate.toLocalDate().toString();
        }
        if (value instanceof java.sql.Time sqlTime) {
            return sqlTime.toLocalTime().toString();
        }
        if (value instanceof Date date) {
            return date.toInstant().toString();
        }
        if (value instanceof BigDecimal decimal) {
            return decimal.toPlainString();
        }
        if (value instanceof Double d && Double.isFinite(d)) {
            return BigDecimal.valueOf(d).toPlainString();
        }
        if (value instanceof Float f && Float.isFinite(f)) {
            return new BigDecimal(Float.toString(f)).toPlainString();
        }
        return String.valueOf(value);
    }

    /** The wire name of a Java property: the first letter upper-cased, the rest untouched. */
    static String propertyName(String javaName) {
        if (javaName == null || javaName.isEmpty()) {
            return javaName;
        }
        return Character.toUpperCase(javaName.charAt(0)) + javaName.substring(1);
    }
}
