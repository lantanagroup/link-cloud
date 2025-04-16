package com.lantanagroup.link.validation.converters;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.matchers.Matcher;
import jakarta.persistence.Converter;

@Converter
public class MatcherConverter extends JsonAttributeConverter<Matcher> {
    public MatcherConverter(ObjectMapper objectMapper) {
        super(objectMapper);
    }

    @Override
    protected Class<Matcher> getAttributeClass() {
        return Matcher.class;
    }
}
