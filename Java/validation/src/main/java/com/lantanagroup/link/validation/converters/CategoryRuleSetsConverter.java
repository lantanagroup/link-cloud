package com.lantanagroup.link.validation.converters;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.model.CategoryRuleSetModel;
import jakarta.persistence.AttributeConverter;

import java.io.IOException;
import java.util.ArrayList;
import java.util.List;

public class CategoryRuleSetsConverter implements AttributeConverter<List<CategoryRuleSetModel>, String> {
    private final static ObjectMapper objectMapper = new ObjectMapper();

    @Override
    public String convertToDatabaseColumn(List<CategoryRuleSetModel> categoryRuleSetModels) {
        try {
            return objectMapper.writeValueAsString(categoryRuleSetModels);
        } catch (JsonProcessingException e) {
            return null;
        }
    }

    @Override
    public List<CategoryRuleSetModel> convertToEntityAttribute(String s) {
        try {
            return objectMapper.readValue(s, objectMapper.getTypeFactory().constructCollectionType(List.class, CategoryRuleSetModel.class));
        } catch (IOException e) {
            return new ArrayList<>();
        }
    }
}
