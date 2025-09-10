package com.lantanagroup.link.validation.configs;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.shared.kafka.ErrorHandler;
import com.lantanagroup.link.shared.kafka.Properties;
import com.lantanagroup.link.shared.kafka.Topics;
import com.lantanagroup.link.validation.records.ReadyForValidation;
import com.lantanagroup.link.validation.records.ValidationComplete;
import io.opentelemetry.instrumentation.kafkaclients.v2_6.TracingConsumerInterceptor;
import io.opentelemetry.instrumentation.kafkaclients.v2_6.TracingProducerInterceptor;
import org.apache.kafka.clients.consumer.ConsumerConfig;
import org.apache.kafka.clients.producer.ProducerConfig;
import org.apache.kafka.common.serialization.*;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.boot.autoconfigure.kafka.KafkaProperties;
import org.springframework.boot.ssl.SslBundles;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.kafka.core.*;
import org.springframework.kafka.listener.CommonErrorHandler;
import org.springframework.kafka.support.serializer.*;

import java.util.Map;
import java.util.regex.Pattern;
import java.util.stream.Collectors;

@Configuration
public class KafkaConfig {
    private static <T> Map<Pattern, T> byPattern(Map<String, T> map) {
        return map.entrySet().stream().collect(Collectors.toMap(
                entry -> Pattern.compile(Pattern.quote(entry.getKey())),
                Map.Entry::getValue));
    }

    @Bean
    public CommonErrorHandler errorHandler() {
        return new ErrorHandler();
    }

    @Bean
    public Deserializer<?> keyDeserializer(ObjectMapper objectMapper) {
        Map<String, Deserializer<?>> deserializers = Map.of(
                Topics.READY_FOR_VALIDATION, new JsonDeserializer<>(ReadyForValidation.Key.class, objectMapper));
        return new ErrorHandlingDeserializer<>(
                new DelegatingByTopicDeserializer(byPattern(deserializers), new VoidDeserializer()));
    }

    @Bean
    public Deserializer<?> valueDeserializer(ObjectMapper objectMapper) {
        Map<String, Deserializer<?>> deserializers = Map.of(
                Topics.READY_FOR_VALIDATION, new JsonDeserializer<>(ReadyForValidation.class, objectMapper));
        return new ErrorHandlingDeserializer<>(
                new DelegatingByTopicDeserializer(byPattern(deserializers), new VoidDeserializer()));
    }

    @Bean
    public ConsumerFactory<?, ?> consumerFactory(
            KafkaProperties properties,
            ObjectProvider<SslBundles> sslBundles,
            Deserializer<?> keyDeserializer,
            Deserializer<?> valueDeserializer) {
        Map<String, Object> consumerProperties = properties.buildConsumerProperties(sslBundles.getIfAvailable());
        consumerProperties.put(ConsumerConfig.INTERCEPTOR_CLASSES_CONFIG, TracingConsumerInterceptor.class.getName());
        return new DefaultKafkaConsumerFactory<>(consumerProperties, keyDeserializer, valueDeserializer);
    }

    @Bean
    public Serializer<?> keySerializer(ObjectMapper objectMapper) {
        Map<String, Serializer<?>> serializers = Map.of(
                Topics.SERVICE_HEALTH_CHECK, new StringSerializer(),
                Topics.VALIDATION_COMPLETE, new StringSerializer());
        return new DelegatingByTopicSerializer(byPattern(serializers), new VoidSerializer());
    }

    @Bean
    public Serializer<?> valueSerializer(ObjectMapper objectMapper) {
        Map<String, Serializer<?>> serializers = Map.of(
                Topics.SERVICE_HEALTH_CHECK, new StringSerializer(),
                Topics.VALIDATION_COMPLETE, getJsonSerializer(objectMapper, ValidationComplete.class));
        return new DelegatingByTopicSerializer(byPattern(serializers), new VoidSerializer());
    }

    private Serializer<?> getJsonSerializer(ObjectMapper objectMapper, Class<?> type) {
        return new JsonSerializer<>(objectMapper.constructType(type), objectMapper).noTypeInfo();
    }

    private <K, V> ProducerFactory<K, V> getProducerFactory(
            KafkaProperties properties,
            ObjectProvider<SslBundles> sslBundles,
            Serializer<K> keySerializer,
            Serializer<V> valueSerializer,
            Map<String, Object> customProperties) {
        Map<String, Object> producerProperties = properties.buildProducerProperties(sslBundles.getIfAvailable());
        producerProperties.putAll(customProperties);
        producerProperties.put(ProducerConfig.INTERCEPTOR_CLASSES_CONFIG, TracingProducerInterceptor.class.getName());
        return new DefaultKafkaProducerFactory<>(producerProperties, keySerializer, valueSerializer);
    }

    @Bean
    public ProducerFactory<?, ?> producerFactory(
            KafkaProperties properties,
            ObjectProvider<SslBundles> sslBundles,
            Serializer<?> keySerializer,
            Serializer<?> valueSerializer) {
        return getProducerFactory(properties, sslBundles, keySerializer, valueSerializer, Map.of());
    }

    @Bean
    public KafkaTemplate<?, ?> defaultKafkaTemplate(
            KafkaProperties properties,
            ObjectProvider<SslBundles> sslBundles,
            Serializer<?> keySerializer,
            Serializer<?> valueSerializer) {
        return new KafkaTemplate<>(getProducerFactory(properties, sslBundles, keySerializer, valueSerializer, Map.of()));
    }

    @Bean
    public KafkaTemplate<String, String> healthKafkaTemplate(
            KafkaProperties properties,
            ObjectProvider<SslBundles> sslBundles,
            Serializer<String> keySerializer,
            Serializer<String> valueSerializer) {
        return new KafkaTemplate<>(getProducerFactory(properties, sslBundles, keySerializer, valueSerializer, Map.of(
                ProducerConfig.MAX_BLOCK_MS_CONFIG, Properties.MAX_BLOCK_MS_CONFIG,
                ProducerConfig.RETRIES_CONFIG, 0)));
    }
}
