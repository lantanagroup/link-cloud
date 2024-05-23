package com.lantanagroup.link.validation;

import com.lantanagroup.link.shared.BaseSpringConfig;
import com.lantanagroup.link.validation.model.PatientEvaluatedModel;
import org.springframework.boot.context.properties.ConfigurationPropertiesScan;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.kafka.config.ConcurrentKafkaListenerContainerFactory;
import org.springframework.kafka.core.ConsumerFactory;
import org.springframework.kafka.listener.CommonErrorHandler;

@Configuration
@ConfigurationPropertiesScan("com.lantanagroup.link.shared.config")
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
}
