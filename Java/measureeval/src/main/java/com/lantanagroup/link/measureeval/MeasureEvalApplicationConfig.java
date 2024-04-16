package com.lantanagroup.link.measureeval;

import com.lantanagroup.link.shared.kafka.KafkaErrorHandler;
import com.lantanagroup.link.shared.mongo.FhirConversions;
import com.lantanagroup.link.shared.security.SecurityHelper;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.data.mongodb.core.convert.MongoCustomConversions;
import org.springframework.kafka.listener.CommonErrorHandler;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.web.SecurityFilterChain;

@Configuration
public class MeasureEvalApplicationConfig {

    @Bean
    CommonErrorHandler commonErrorHandler() {
        return new KafkaErrorHandler();
    }

    /**
     * @Bean ConcurrentKafkaListenerContainerFactory<String, PatientEvaluatedModel> kafkaListenerContainerFactory(
     * ConsumerFactory<String, PatientEvaluatedModel> consumerFactory,
     * CommonErrorHandler commonErrorHandler
     * ) {
     * ConcurrentKafkaListenerContainerFactory<String, PatientEvaluatedModel> factory = new ConcurrentKafkaListenerContainerFactory<>();
     * factory.setConsumerFactory(consumerFactory);
     * factory.setCommonErrorHandler(commonErrorHandler);
     * return factory;
     * }
     */

    @Bean
    SecurityFilterChain web(HttpSecurity http) throws Exception {
        return SecurityHelper.build(http);
    }

    @Bean
    MongoCustomConversions customConversions() {
        return new FhirConversions();
    }
}
