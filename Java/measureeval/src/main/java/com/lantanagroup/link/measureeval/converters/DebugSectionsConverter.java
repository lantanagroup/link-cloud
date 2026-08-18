package com.lantanagroup.link.measureeval.converters;

import com.lantanagroup.link.measureeval.models.DebugSections;
import org.springframework.core.convert.converter.Converter;
import org.springframework.stereotype.Component;

import java.util.Set;

/**
 * Adapts the {@code ?debug=...} query parameter from raw String to {@code Set<DebugSections>}
 * at the Spring MVC request-binding edge. Lets controllers declare the strongly-typed parameter
 * directly instead of parsing the value inline.
 *
 * <p>Registered via {@link com.lantanagroup.link.measureeval.configs.WebMvcConfig}.
 * Delegates parsing to {@link DebugSections#parse(String)} so the wire contract stays in one place.
 */
@Component
public class DebugSectionsConverter implements Converter<String, Set<DebugSections>> {

    @Override
    public Set<DebugSections> convert(String source) {
        return DebugSections.parse(source);
    }
}
