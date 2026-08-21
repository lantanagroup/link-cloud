package com.lantanagroup.link.validation.configs;

import com.lantanagroup.link.validation.enums.RollupStrategy;
import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.context.annotation.Configuration;

@Configuration
@ConfigurationProperties("validation")
@Getter
@Setter
public class ValidationPolicyConfig {

    private CategoryOverride categoryOverride = new CategoryOverride();
    private Scoring scoring = new Scoring();
    private Response response = new Response();

    @Getter
    @Setter
    public static class CategoryOverride {

        private boolean enabled = false;
    }

    @Getter
    @Setter
    public static class Scoring {

        private RollupStrategy rollup;
    }

    @Getter
    @Setter
    public static class Response {

        private boolean includeOriginalSeverity = true;

        private boolean includeCategoryIds = true;
    }
}
