package com.lantanagroup.link.shared.audit;

import java.lang.reflect.Array;
import java.math.BigDecimal;
import java.util.Base64;
import java.util.Collection;
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
     * {@code toString()}. Binary is base64, as the .NET services write it.
     */
    static String text(Object value) {
        if (value == null) {
            return "";
        }
        if (value instanceof byte[] bytes) {
            return Base64.getEncoder().encodeToString(bytes);
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

    /**
     * Whether a value is a list of values: an array or a collection. Bytes and characters are not; they are one
     * value each, binary and text.
     */
    static boolean isList(Object value) {
        if (value instanceof Collection<?>) {
            return true;
        }
        return value != null && value.getClass().isArray() && !(value instanceof byte[]) && !(value instanceof char[]);
    }

    /**
     * A list of simple values as a JSON array, which is how the .NET services write one: numbers and booleans
     * bare, everything else as a string of {@link #text}, so a date or an enum reads here as it does anywhere
     * else in an event. Null for null, so an absent list reads as empty and not as an empty array.
     */
    static String jsonArray(Object list) {
        if (list == null) {
            return null;
        }
        StringBuilder json = new StringBuilder();
        appendJson(json, list);
        return json.toString();
    }

    private static void appendJson(StringBuilder json, Object value) {
        if (value == null) {
            json.append("null");
        } else if (value instanceof Collection<?> items) {
            json.append('[');
            String separator = "";
            for (Object item : items) {
                json.append(separator);
                appendJson(json, item);
                separator = ",";
            }
            json.append(']');
        } else if (isList(value)) {
            json.append('[');
            for (int i = 0; i < Array.getLength(value); i++) {
                if (i > 0) {
                    json.append(',');
                }
                appendJson(json, Array.get(value, i));
            }
            json.append(']');
        } else if (value instanceof Boolean || isJsonNumber(value)) {
            json.append(text(value));
        } else {
            appendJsonString(json, text(value));
        }
    }

    /** A number JSON can hold: any but the not-a-number and infinite values, which read as strings. */
    private static boolean isJsonNumber(Object value) {
        if (value instanceof Double d) {
            return Double.isFinite(d);
        }
        if (value instanceof Float f) {
            return Float.isFinite(f);
        }
        return value instanceof Number;
    }

    private static void appendJsonString(StringBuilder json, String text) {
        json.append('"');
        for (int i = 0; i < text.length(); i++) {
            char c = text.charAt(i);
            switch (c) {
                case '"' -> json.append("\\\"");
                case '\\' -> json.append("\\\\");
                case '\n' -> json.append("\\n");
                case '\r' -> json.append("\\r");
                case '\t' -> json.append("\\t");
                default -> {
                    if (c < 0x20) {
                        json.append(String.format("\\u%04x", (int) c));
                    } else {
                        json.append(c);
                    }
                }
            }
        }
        json.append('"');
    }

    /** The wire name of a Java property: the first letter upper-cased, the rest untouched. */
    static String propertyName(String javaName) {
        if (javaName == null || javaName.isEmpty()) {
            return javaName;
        }
        return Character.toUpperCase(javaName.charAt(0)) + javaName.substring(1);
    }
}
