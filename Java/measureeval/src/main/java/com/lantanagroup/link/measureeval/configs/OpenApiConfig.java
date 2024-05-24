package com.lantanagroup.link.measureeval.configs;

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
                .info(new Info().title("Measure Evaluation Service")
                        .description("Service for evaluating measures against clinical data.")
                        .version(version)
                        .license(new License().name("Apache 2.0")));
    }
}
