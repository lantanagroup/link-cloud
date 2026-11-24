package com.lantanagroup.link.shared.audit;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.core.SerializableString;
import com.fasterxml.jackson.core.io.CharacterEscapes;
import com.fasterxml.jackson.core.json.JsonWriteFeature;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.ObjectWriter;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.databind.json.JsonMapper;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;

import java.util.TimeZone;

/**
 * The JSON settings of {@code AuditableEventOccurred}, independent of any service's Jackson configuration.
 */
final class AuditJson {
    private static final ObjectWriter LOG_WRITER = wireMapper().writer()
            .with(JsonWriteFeature.ESCAPE_NON_ASCII)
            .with(new DeleteEscapes());

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
