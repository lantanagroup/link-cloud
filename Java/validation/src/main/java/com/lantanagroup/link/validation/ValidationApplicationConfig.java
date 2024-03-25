package com.lantanagroup.link.validation;

import com.lantanagroup.link.validation.kafka.KafkaErrorHandler;
import com.lantanagroup.link.validation.model.PatientEvaluatedModel;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.kafka.config.ConcurrentKafkaListenerContainerFactory;
import org.springframework.kafka.core.ConsumerFactory;
import org.springframework.kafka.listener.CommonErrorHandler;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.web.SecurityFilterChain;

@Configuration
public class ValidationApplicationConfig {

    @Bean
    CommonErrorHandler commonErrorHandler() {
        return new KafkaErrorHandler();
    }

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
    SecurityFilterChain web(HttpSecurity http) throws Exception {
        // TODO: This needs to be updated to secure the application
        http
                .authorizeHttpRequests((authorizeRequests) ->
                        authorizeRequests
                                .anyRequest()
                                .anonymous());
        return http.build();
    }
}
