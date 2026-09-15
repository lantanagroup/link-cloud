package com.lantanagroup.link.measureeval.configs;

import com.lantanagroup.link.measureeval.converters.DebugSectionsConverter;
import org.springframework.context.annotation.Configuration;
import org.springframework.format.FormatterRegistry;
import org.springframework.web.servlet.config.annotation.WebMvcConfigurer;

/**
 * Registers project-specific Spring MVC converters/formatters so controllers can declare
 * strongly-typed request parameters instead of taking raw Strings and parsing inline.
 */
@Configuration
public class WebMvcConfig implements WebMvcConfigurer {

    private final DebugSectionsConverter debugSectionsConverter;

    public WebMvcConfig(DebugSectionsConverter debugSectionsConverter) {
        this.debugSectionsConverter = debugSectionsConverter;
    }

    @Override
    public void addFormatters(FormatterRegistry registry) {
        registry.addConverter(debugSectionsConverter);
    }
}
