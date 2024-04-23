package com.lantanagroup.link.measureeval.config;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.measureeval.Topics;
import com.lantanagroup.link.measureeval.records.ResourceNormalized;
import org.apache.kafka.common.serialization.Deserializer;
import org.apache.kafka.common.serialization.Serializer;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.boot.autoconfigure.kafka.KafkaProperties;
import org.springframework.boot.ssl.SslBundles;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.kafka.core.ConsumerFactory;
import org.springframework.kafka.core.DefaultKafkaConsumerFactory;
import org.springframework.kafka.core.DefaultKafkaProducerFactory;
import org.springframework.kafka.core.ProducerFactory;
import org.springframework.kafka.support.serializer.ErrorHandlingDeserializer;
import org.springframework.kafka.support.serializer.JsonDeserializer;
import org.springframework.kafka.support.serializer.JsonSerializer;

import java.util.Map;

@Configuration
public class KafkaConfig {
    @Bean
    public Deserializer<?> valueDeserializer(ObjectMapper objectMapper) {
        JsonDeserializer<?> jsonDeserializer = new JsonDeserializer<>(objectMapper);
        jsonDeserializer.setTypeResolver((topic, data, headers) ->
                switch (topic) {
                    case Topics.RESOURCE_NORMALIZED -> objectMapper.constructType(ResourceNormalized.class);
                    default -> throw new IllegalArgumentException("Unknown topic: " + topic);
                });
        return new ErrorHandlingDeserializer<>(jsonDeserializer);
    }

    @Bean
    public Serializer<?> valueSerializer(ObjectMapper objectMapper) {
        return new JsonSerializer<>(objectMapper);
    }

    @Bean
    public ConsumerFactory<?, ?> consumerFactory(
            KafkaProperties properties,
            ObjectProvider<SslBundles> sslBundles,
            Deserializer<?> valueDeserializer) {
        Map<String, Object> consumerProperties = properties.buildConsumerProperties(sslBundles.getIfAvailable());
        return new DefaultKafkaConsumerFactory<>(consumerProperties, null, valueDeserializer);
    }

    @Bean
    public ProducerFactory<?, ?> producerFactory(
            KafkaProperties properties,
            ObjectProvider<SslBundles> sslBundles,
            Serializer<?> valueSerializer) {
        Map<String, Object> producerProperties = properties.buildProducerProperties(sslBundles.getIfAvailable());
        return new DefaultKafkaProducerFactory<>(producerProperties, null, valueSerializer);
    }
}
