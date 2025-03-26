package com.lantanagroup.link.validation.configs;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.shared.kafka.ErrorHandler;
import com.lantanagroup.link.shared.kafka.Topics;
import com.lantanagroup.link.validation.records.ReadyForValidation;
import com.lantanagroup.link.validation.records.ValidationComplete;
import org.apache.kafka.common.serialization.Deserializer;
import org.apache.kafka.common.serialization.Serializer;
import org.apache.kafka.common.serialization.VoidDeserializer;
import org.apache.kafka.common.serialization.VoidSerializer;
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
        return new DefaultKafkaConsumerFactory<>(consumerProperties, keyDeserializer, valueDeserializer);
    }

    @Bean
    public Serializer<?> keySerializer(ObjectMapper objectMapper) {
        Map<String, Serializer<?>> serializers = Map.of(
                Topics.VALIDATION_COMPLETE, getJsonSerializer(objectMapper, String.class));
        return new DelegatingByTopicSerializer(byPattern(serializers), new VoidSerializer());
    }

    @Bean
    public Serializer<?> valueSerializer(ObjectMapper objectMapper) {
        Map<String, Serializer<?>> serializers = Map.of(
                Topics.VALIDATION_COMPLETE, getJsonSerializer(objectMapper, ValidationComplete.class));
        return new DelegatingByTopicSerializer(byPattern(serializers), new VoidSerializer());
    }

    private Serializer<?> getJsonSerializer(ObjectMapper objectMapper, Class<?> type) {
        return new JsonSerializer<>(objectMapper.constructType(type), objectMapper).noTypeInfo();
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
