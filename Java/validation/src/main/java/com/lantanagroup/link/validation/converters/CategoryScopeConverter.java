package com.lantanagroup.link.validation.converters;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.CategoryScope;
import jakarta.persistence.Converter;

/**
 * JPA {@code AttributeConverter} that persists {@link CategoryScope} instances as JSON in the
 * {@code category.scope} column.
 *
 * <p>Unlike {@link MatcherConverter}, scope is nullable — only {@code SKIP} rules carry one —
 * so this converter explicitly maps {@code null} to {@code null} on both directions to keep
 * the DB column truly empty for LABEL/SUPPRESS rules rather than storing the literal
 * string {@code "null"}.</p>
 */
@Converter
public class CategoryScopeConverter extends JsonAttributeConverter<CategoryScope> {
    public CategoryScopeConverter(ObjectMapper objectMapper) {
        super(objectMapper);
    }

    @Override
    public String convertToDatabaseColumn(CategoryScope model) {
        return model == null ? null : super.convertToDatabaseColumn(model);
    }

    @Override
    public CategoryScope convertToEntityAttribute(String json) {
        return (json == null || json.isBlank()) ? null : super.convertToEntityAttribute(json);
    }

    @Override
    protected Class<CategoryScope> getAttributeClass() {
        return CategoryScope.class;
    }
}
