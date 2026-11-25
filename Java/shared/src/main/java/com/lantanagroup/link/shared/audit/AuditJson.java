package com.lantanagroup.link.shared.audit;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.core.SerializableString;
import com.fasterxml.jackson.core.io.CharacterEscapes;
import com.fasterxml.jackson.core.json.JsonWriteFeature;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.ObjectWriter;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.databind.json.JsonMapper;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import org.springframework.core.NestedExceptionUtils;

import java.util.HashSet;
import java.util.LinkedHashSet;
import java.util.Locale;
import java.util.Set;
import java.util.TimeZone;

/**
 * The JSON settings of {@code AuditableEventOccurred}, independent of any service's Jackson configuration.
 */
final class AuditJson {
    private static final ObjectWriter LOG_WRITER = wireMapper().writer()
            .with(JsonWriteFeature.ESCAPE_NON_ASCII)
            .with(new DeleteEscapes());
    private static final ObjectMapper TREE_READER = new ObjectMapper();

    private AuditJson() {
    }

    /**
     * A new mapper configured for the wire. Every setting the wire depends on is stated, so no Jackson
     * default, and nothing a service configures on its own mapper, can change what is written.
     */
    static ObjectMapper wireMapper() {
        return JsonMapper.builder()
                .addModule(new JavaTimeModule())
                .disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS)
                .defaultTimeZone(TimeZone.getTimeZone("UTC"))
                .serializationInclusion(JsonInclude.Include.ALWAYS)
                .propertyNamingStrategy(null)
                .build();
    }

    /**
     * Where two JSON texts differ: the path of every value that is not the same in both. A path is the field
     * names leading to the value, each as {@link #pathName} writes it and after a slash; positions in an array are left out, so every
     * item of a list shares the list's path. A field that is absent and one that is null are the same. An array
     * that changed length, or a value that changed kind, differs as a whole at its own path.
     * <p>
     * Null when either text cannot be read as JSON. Never throws.
     */
    static Set<String> differingPaths(String before, String after) {
        try {
            Set<String> paths = new HashSet<>();
            collectDifferences(before == null ? null : TREE_READER.readTree(before),
                    after == null ? null : TREE_READER.readTree(after), "", paths);
            return paths;
        } catch (Exception | StackOverflowError e) {
            return null;
        }
    }

    /**
     * A field or property name as it appears in a path: lower-cased, with anything but letters and digits left
     * out, so a property and the field a naming strategy stores it under ({@code startHour} as
     * {@code start_hour}) read the same.
     */
    static String pathName(String name) {
        return name.toLowerCase(Locale.ROOT).replaceAll("[^\\p{L}\\p{Nd}]", "");
    }

    private static void collectDifferences(JsonNode before, JsonNode after, String path, Set<String> paths) {
        boolean noBefore = before == null || before.isNull() || before.isMissingNode();
        boolean noAfter = after == null || after.isNull() || after.isMissingNode();
        if (noBefore && noAfter) {
            return;
        }
        if ((noBefore || before.isObject()) && (noAfter || after.isObject())) {
            Set<String> names = new LinkedHashSet<>();
            if (!noBefore) {
                before.fieldNames().forEachRemaining(names::add);
            }
            if (!noAfter) {
                after.fieldNames().forEachRemaining(names::add);
            }
            for (String name : names) {
                collectDifferences(noBefore ? null : before.get(name), noAfter ? null : after.get(name),
                        path + "/" + pathName(name), paths);
            }
            return;
        }
        if (!noBefore && !noAfter && before.isArray() && after.isArray() && before.size() == after.size()) {
            for (int i = 0; i < before.size(); i++) {
                collectDifferences(before.get(i), after.get(i), path, paths);
            }
            return;
        }
        if (noBefore || noAfter || !before.equals(after)) {
            paths.add(path);
        }
    }

    /**
     * The event as one line of JSON, for a log. Non-ASCII characters are escaped and line breaks inside
     * values are written as {@code \n}, so the line cannot be split or forged. Never throws.
     */
    static String logLine(AuditEventMessage event) {
        try {
            return LOG_WRITER.writeValueAsString(event);
        } catch (Exception e) {
            return String.valueOf(event).replace('\r', ' ').replace('\n', ' ');
        }
    }

    /**
     * A failure as text for a log, in place of the exception itself: the failure and, when it differs, its
     * most specific cause, escaped as the event line is. Spring Kafka fails the send future with a
     * {@code KafkaProducerException} whose message does not include the cause, so the cause is what says why.
     * An exception message can carry text from a request or a record (a serialiser's message quotes the
     * value), so control and non-ASCII characters are escaped and the line cannot be split or forged.
     * Never throws.
     */
    static String describe(Throwable failure) {
        if (failure == null) {
            return null;
        }
        try {
            StringBuilder text = new StringBuilder(summary(failure));
            Throwable cause = NestedExceptionUtils.getMostSpecificCause(failure);
            if (cause != failure) {
                text.append(" caused by ").append(summary(cause));
            }
            return escape(text.toString());
        } catch (RuntimeException e) {
            return failure.getClass().getName();
        }
    }

    private static String summary(Throwable failure) {
        String name = failure.getClass().getSimpleName();
        if (name.isEmpty()) {
            name = failure.getClass().getName();
        }
        String message = failure.getMessage();
        return message == null ? name : name + ": " + message;
    }

    /**
     * Where a failure was thrown, as the first frame of its stack trace: a class, a method and a line, none of
     * which a caller supplies. It stands in for the stack trace in a log line that does not carry the
     * exception. Null when the failure has no stack trace. Never throws.
     */
    static String thrownAt(Throwable failure) {
        try {
            StackTraceElement[] frames = failure == null ? null : failure.getStackTrace();
            return frames == null || frames.length == 0 ? null : escape(frames[0].toString());
        } catch (RuntimeException e) {
            return null;
        }
    }

    /**
     * The text escaped as {@link #logLine} escapes a value, without the surrounding quotes: every control
     * and non-ASCII character becomes a JSON escape (a line feed as backslash-n, the rest as a four-digit
     * backslash-u escape), so the text cannot split or forge a log entry. Never throws.
     */
    static String escape(String text) {
        if (text == null) {
            return null;
        }
        try {
            String quoted = LOG_WRITER.writeValueAsString(text);
            return quoted.substring(1, quoted.length() - 1);
        } catch (Exception e) {
            return text.replaceAll("[^\\x20-\\x7e]", "?");
        }
    }

    /**
     * JSON's standard escapes plus DEL (U+007F), the one control character that is neither below U+0020
     * nor above the ASCII range {@code ESCAPE_NON_ASCII} covers.
     */
    private static final class DeleteEscapes extends CharacterEscapes {
        private final int[] escapes;

        DeleteEscapes() {
            escapes = CharacterEscapes.standardAsciiEscapesForJSON();
            escapes[0x7f] = CharacterEscapes.ESCAPE_STANDARD;
        }

        @Override
        public int[] getEscapeCodesForAscii() {
            return escapes;
        }

        @Override
        public SerializableString getEscapeSequence(int ch) {
            return null;
        }
    }
}
