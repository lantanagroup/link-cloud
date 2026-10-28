package com.lantanagroup.link.validation.configs;

import io.swagger.v3.oas.models.Components;
import io.swagger.v3.oas.models.OpenAPI;
import io.swagger.v3.oas.models.info.Info;
import io.swagger.v3.oas.models.info.License;
import io.swagger.v3.oas.models.security.SecurityScheme;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class OpenApiConfig {
    @Bean
    public OpenAPI openApi() {
        String version = this.getClass().getPackage().getImplementationVersion() != null ?
                this.getClass().getPackage().getImplementationVersion() :
                "dev";

        return new OpenAPI()
                .components(
                        new Components()
                                .addSecuritySchemes(
                                        "bearer-key",
                                        new SecurityScheme()
                                                .type(SecurityScheme.Type.HTTP)
                                                .scheme("bearer")
                                                .bearerFormat("JWT")))
                .info(new Info().title("Validation Service")
                        .description("Service for validating FHIR resources against FHIR conformance resources.")
                        .version(version)
                        .license(new License().name("Apache 2.0")));
    }
}
