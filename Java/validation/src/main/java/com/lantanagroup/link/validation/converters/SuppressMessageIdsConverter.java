package com.lantanagroup.link.validation.converters;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import jakarta.persistence.AttributeConverter;
import jakarta.persistence.Converter;

import java.io.UncheckedIOException;
import java.util.List;

/**
 * JPA {@code AttributeConverter} that persists {@code Category.suppressMessageIds} as a JSON
 * array in a single {@code varchar(max)} column.
 *
 * <p>Unlike {@link MatcherConverter}, the wrapped type is a parameterized {@code List<String>},
 * which means the base {@link JsonAttributeConverter} pattern (which uses a raw {@code Class<T>})
 * doesn't directly apply. This converter uses a Jackson {@link TypeReference} instead so the
 * deserialized list is genuinely {@code List<String>} rather than {@code List<Object>} (which
 * would happen if we passed {@code List.class} as the deserialization hint).</p>
 *
 * <p>Null and empty lists map to {@code null} in the DB column — same pattern as
 * {@link CategoryScopeConverter}, so rules without SUPPRESS coverage carry a genuinely empty
 * column rather than the literal string {@code "null"} or {@code "[]"}.</p>
 */
@Converter
public class SuppressMessageIdsConverter implements AttributeConverter<List<String>, String> {
    private static final TypeReference<List<String>> LIST_OF_STRING = new TypeReference<>() {};

    private final ObjectMapper objectMapper;

    public SuppressMessageIdsConverter(ObjectMapper objectMapper) {
        this.objectMapper = objectMapper;
    }

    @Override
    public String convertToDatabaseColumn(List<String> model) {
        if (model == null || model.isEmpty()) {
            return null;
        }
        try {
            return objectMapper.writeValueAsString(model);
        } catch (JsonProcessingException e) {
            throw new UncheckedIOException(e);
        }
    }

    @Override
    public List<String> convertToEntityAttribute(String json) {
        if (json == null || json.isBlank()) {
            return null;
        }
        try {
            return objectMapper.readValue(json, LIST_OF_STRING);
        } catch (JsonProcessingException e) {
            throw new UncheckedIOException(e);
        }
    }
}
