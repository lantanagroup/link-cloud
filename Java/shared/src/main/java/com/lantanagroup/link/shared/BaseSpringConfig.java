package com.lantanagroup.link.shared;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.shared.fhir.FhirObjectMapper;
import com.lantanagroup.link.shared.kafka.KafkaErrorHandler;
import com.lantanagroup.link.shared.mongo.FhirConversions;
import com.lantanagroup.link.shared.security.SecurityHelper;
import org.springframework.context.annotation.Bean;
import org.springframework.data.mongodb.core.convert.MongoCustomConversions;
import org.springframework.kafka.listener.CommonErrorHandler;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.web.SecurityFilterChain;

public class BaseSpringConfig {

    @Bean
    CommonErrorHandler commonErrorHandler() {
        return new KafkaErrorHandler();
    }

    @Bean
    SecurityFilterChain web(HttpSecurity http) throws Exception {
        return SecurityHelper.buildAnonymous(http);
    }

    @Bean
    MongoCustomConversions customConversions() {
        return new FhirConversions();
    }

    @Bean
    ObjectMapper objectMapper() {
        return new FhirObjectMapper();
    }
}
