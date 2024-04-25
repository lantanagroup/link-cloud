package com.lantanagroup.link.validation;

import com.lantanagroup.link.shared.BaseSpringConfig;
import com.lantanagroup.link.validation.model.PatientEvaluatedModel;
import io.swagger.v3.oas.models.OpenAPI;
import io.swagger.v3.oas.models.info.Info;
import io.swagger.v3.oas.models.info.License;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.kafka.config.ConcurrentKafkaListenerContainerFactory;
import org.springframework.kafka.core.ConsumerFactory;
import org.springframework.kafka.listener.CommonErrorHandler;

@Configuration
public class ValidationApplicationConfig extends BaseSpringConfig {
    @Bean
    ConcurrentKafkaListenerContainerFactory<String, PatientEvaluatedModel> kafkaListenerContainerFactory(
            ConsumerFactory<String, PatientEvaluatedModel> consumerFactory,
            CommonErrorHandler commonErrorHandler
    ) {
        ConcurrentKafkaListenerContainerFactory<String, PatientEvaluatedModel> factory = new ConcurrentKafkaListenerContainerFactory<>();
        factory.setConsumerFactory(consumerFactory);
        factory.setCommonErrorHandler(commonErrorHandler);
        return factory;
    }

    @Bean
    public OpenAPI openApi() {
        String version = this.getClass().getPackage().getImplementationVersion() != null ?
                this.getClass().getPackage().getImplementationVersion() :
                "dev";

        return new OpenAPI()
                .info(new Info().title("Validation Service")
                        .description("Service for validating FHIR resources against FHIR conformance resources.")
                        .version(version)
                        .license(new License().name("Apache 2.0")));
    }
}
