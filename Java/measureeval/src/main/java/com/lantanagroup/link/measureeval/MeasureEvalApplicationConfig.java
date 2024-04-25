package com.lantanagroup.link.measureeval;

import com.lantanagroup.link.shared.BaseSpringConfig;
import org.springframework.context.annotation.Configuration;

@Configuration
public class MeasureEvalApplicationConfig extends BaseSpringConfig {

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
}
