package com.lantanagroup.link.measureeval.configs;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.measureeval.kafka.ErrorHandler;
import com.lantanagroup.link.measureeval.kafka.Topics;
import com.lantanagroup.link.measureeval.records.DataAcquisitionRequested;
import com.lantanagroup.link.measureeval.records.ResourceAcquired;
import com.lantanagroup.link.measureeval.records.ResourceEvaluated;
import com.lantanagroup.link.measureeval.records.ResourceNormalized;
import com.lantanagroup.link.measureeval.utils.StreamUtils;
import org.apache.kafka.common.serialization.*;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.boot.autoconfigure.kafka.KafkaProperties;
import org.springframework.boot.ssl.SslBundles;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.kafka.core.ConsumerFactory;
import org.springframework.kafka.core.DefaultKafkaConsumerFactory;
import org.springframework.kafka.core.DefaultKafkaProducerFactory;
import org.springframework.kafka.core.ProducerFactory;
import org.springframework.kafka.listener.CommonErrorHandler;
import org.springframework.kafka.support.serializer.*;

import java.util.Map;
import java.util.regex.Pattern;

@Configuration
public class KafkaConfig {
    // TODO: Replace this and StreamUtils.mapKeys with getDeserializers
    private static Pattern getPattern(String topic) {
        return Pattern.compile(Pattern.quote(topic));
    }

    @Bean
    public CommonErrorHandler errorHandler() {
        return new ErrorHandler();
    }

    @Bean
    public Deserializer<?> keyDeserializer(ObjectMapper objectMapper) {
        Map<String, Deserializer<?>> deserializers = Map.of(
                Topics.error(Topics.RESOURCE_ACQUIRED), new StringDeserializer(),
                Topics.RESOURCE_NORMALIZED, new StringDeserializer());
        return new ErrorHandlingDeserializer<>(new DelegatingByTopicDeserializer(
                StreamUtils.mapKeys(deserializers, KafkaConfig::getPattern),
                new VoidDeserializer()));
    }

    @Bean
    public Deserializer<?> valueDeserializer(ObjectMapper objectMapper) {
        Map<String, Deserializer<?>> deserializers = Map.of(
                Topics.error(Topics.RESOURCE_ACQUIRED), new JsonDeserializer<>(ResourceAcquired.class, objectMapper),
                Topics.RESOURCE_NORMALIZED, new JsonDeserializer<>(ResourceNormalized.class, objectMapper));
        return new ErrorHandlingDeserializer<>(new DelegatingByTopicDeserializer(
                StreamUtils.mapKeys(deserializers, KafkaConfig::getPattern),
                new VoidDeserializer()));
    }

    @Bean
    public ConsumerFactory<?, ?> consumerFactory(
            KafkaProperties properties,
            ObjectProvider<SslBundles> sslBundles,
            Deserializer<?> keyDeserializer,
            Deserializer<?> valueDeserializer) {
        Map<String, Object> consumerProperties = properties.buildConsumerProperties(sslBundles.getIfAvailable());
        return new DefaultKafkaConsumerFactory<>(consumerProperties, keyDeserializer, valueDeserializer);
    }

    @Bean
    public Serializer<?> keySerializer(ObjectMapper objectMapper) {
        Map<String, Serializer<?>> serializers = Map.of(
                Topics.DATA_ACQUISITION_REQUESTED, new StringSerializer(),
                Topics.RESOURCE_EVALUATED, new JsonSerializer<>(
                        objectMapper.constructType(ResourceEvaluated.Key.class),
                        objectMapper));
        return new DelegatingByTopicSerializer(
                StreamUtils.mapKeys(serializers, KafkaConfig::getPattern),
                new VoidSerializer());
    }

    @Bean
    public Serializer<?> valueSerializer(ObjectMapper objectMapper) {
        Map<String, Serializer<?>> serializers = Map.of(
                Topics.DATA_ACQUISITION_REQUESTED, new JsonSerializer<>(
                        objectMapper.constructType(DataAcquisitionRequested.class),
                        objectMapper),
                Topics.RESOURCE_EVALUATED, new JsonSerializer<>(
                        objectMapper.constructType(ResourceEvaluated.class),
                        objectMapper));
        return new DelegatingByTopicSerializer(
                StreamUtils.mapKeys(serializers, KafkaConfig::getPattern),
                new VoidSerializer());
    }

    @Bean
    public ProducerFactory<?, ?> producerFactory(
            KafkaProperties properties,
            ObjectProvider<SslBundles> sslBundles,
            Serializer<?> keySerializer,
            Serializer<?> valueSerializer) {
        Map<String, Object> producerProperties = properties.buildProducerProperties(sslBundles.getIfAvailable());
        return new DefaultKafkaProducerFactory<>(producerProperties, keySerializer, valueSerializer);
    }
}
