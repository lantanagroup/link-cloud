package com.lantanagroup.link.validation;

import com.lantanagroup.link.validation.kafka.KafkaProperties;
import com.lantanagroup.link.validation.model.PatientEvaluatedModel;
import com.lantanagroup.link.validation.serdes.PatientEvaluatedDeserializer;
import org.apache.kafka.common.serialization.StringDeserializer;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.Banner;
import org.springframework.boot.SpringApplication;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.PropertySource;
import org.springframework.kafka.core.ConsumerFactory;
import org.springframework.kafka.core.DefaultKafkaConsumerFactory;

import java.util.HashMap;
import java.util.Map;

@SpringBootApplication
@PropertySource("classpath:application.yml")
public class ValidationApplication {
    @Autowired
    private KafkaProperties kafkaProperties;

    public static void main(String[] args) {
        SpringApplication application = new SpringApplication(ValidationApplication.class);
        application.setWebApplicationType(WebApplicationType.NONE);
        application.setBannerMode(Banner.Mode.OFF);
        application.run(args);
    }

    @Bean
    public ConsumerFactory<String, PatientEvaluatedModel> patientEvaluatedConsumerFactory() {
        Map<String, Object> props = new HashMap<>(kafkaProperties.buildConsumerProperties());
        return new DefaultKafkaConsumerFactory<>(props, new StringDeserializer(), new PatientEvaluatedDeserializer());
    }
}
