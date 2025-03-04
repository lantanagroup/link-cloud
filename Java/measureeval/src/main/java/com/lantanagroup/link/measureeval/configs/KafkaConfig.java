package com.lantanagroup.link.measureeval.configs;

import com.fasterxml.jackson.databind.JavaType;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.shared.exceptions.FhirParseException;
import com.lantanagroup.link.shared.exceptions.ValidationException;
import com.lantanagroup.link.shared.kafka.ErrorHandler;
import com.lantanagroup.link.shared.kafka.Topics;
import com.lantanagroup.link.measureeval.records.*;
import io.opentelemetry.instrumentation.kafkaclients.v2_6.TracingConsumerInterceptor;
import io.opentelemetry.instrumentation.kafkaclients.v2_6.TracingProducerInterceptor;
import org.apache.kafka.clients.consumer.ConsumerConfig;
import org.apache.kafka.clients.producer.ProducerConfig;
import org.apache.kafka.common.header.Headers;
import org.apache.kafka.common.serialization.*;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.boot.autoconfigure.kafka.KafkaProperties;
import org.springframework.boot.ssl.SslBundles;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.kafka.core.*;
import org.springframework.kafka.listener.CommonErrorHandler;
import org.springframework.kafka.retrytopic.RetryTopicConfiguration;
import org.springframework.kafka.retrytopic.RetryTopicConfigurationBuilder;
import org.springframework.kafka.support.serializer.*;

import java.text.SimpleDateFormat;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.regex.Pattern;
import java.util.stream.Collectors;
import com.lantanagroup.link.shared.config.KafkaRetryConfig;
import org.springframework.messaging.MessageHandlingException;

@Configuration
public class KafkaConfig {

    private final KafkaRetryConfig KafkaRetryConfig;

    public KafkaConfig(KafkaRetryConfig KafkaRetryConfig) {
        this.KafkaRetryConfig = KafkaRetryConfig;
    }

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
                Topics.RESOURCE_ACQUIRED_ERROR, new StringDeserializer(),
                Topics.RESOURCE_NORMALIZED, new StringDeserializer(),
                Topics.RESOURCE_NORMALIZED_ERROR, new StringDeserializer(),
                Topics.RESOURCE_NORMALIZED_RETRY, new StringDeserializer(),
                Topics.EVALUATION_REQUESTED, new StringDeserializer(),
                Topics.EVALUATION_REQUESTED_ERROR, new StringDeserializer(),
                Topics.EVALUATION_REQUESTED_RETRY, new StringDeserializer());
        return new ErrorHandlingDeserializer<>(
                new DelegatingByTopicDeserializer(byPattern(deserializers), new StringDeserializer()));
    }

    @Bean
    public Deserializer<?> valueDeserializer(ObjectMapper objectMapper) {
        Map<String, Deserializer<?>> deserializers = Map.of(
                Topics.RESOURCE_ACQUIRED_ERROR, new JsonDeserializer<>(ResourceAcquired.class, objectMapper)
                        .trustedPackages("*")
                        .ignoreTypeHeaders()
                        .typeResolver(KafkaConfig::resolveType),
                Topics.RESOURCE_NORMALIZED, new JsonDeserializer<>(ResourceNormalized.class, objectMapper)
                        .trustedPackages("*")
                        .ignoreTypeHeaders()
                        .typeResolver(KafkaConfig::resolveType),
                Topics.RESOURCE_NORMALIZED_ERROR, new JsonDeserializer<>(ResourceNormalized.class, objectMapper)
                        .trustedPackages("*")
                        .ignoreTypeHeaders()
                        .typeResolver(KafkaConfig::resolveType),
                Topics.RESOURCE_EVALUATED, new JsonDeserializer<>(ResourceEvaluated.class, objectMapper)
                        .trustedPackages("*")
                        .ignoreTypeHeaders()
                        .typeResolver(KafkaConfig::resolveType),
                Topics.RESOURCE_NORMALIZED_RETRY, new JsonDeserializer<>(ResourceNormalized.class, objectMapper)
                        .trustedPackages("*")
                        .ignoreTypeHeaders()
                        .typeResolver(KafkaConfig::resolveType),
                Topics.EVALUATION_REQUESTED, new JsonDeserializer<>(EvaluationRequested.class, objectMapper)
                        .trustedPackages("*")
                        .ignoreTypeHeaders()
                        .typeResolver(KafkaConfig::resolveType),
                Topics.EVALUATION_REQUESTED_ERROR, new JsonDeserializer<>(EvaluationRequested.class, objectMapper)
                        .trustedPackages("*")
                        .ignoreTypeHeaders()
                        .typeResolver(KafkaConfig::resolveType),
                Topics.EVALUATION_REQUESTED_RETRY, new JsonDeserializer<>(EvaluationRequested.class, objectMapper)
                        .trustedPackages("*")
                        .ignoreTypeHeaders()
                        .typeResolver(KafkaConfig::resolveType));

        return new ErrorHandlingDeserializer<>(
                new DelegatingByTopicDeserializer(byPattern(deserializers), new JsonDeserializer<Object>().trustedPackages("*").ignoreTypeHeaders().typeResolver(KafkaConfig::resolveType)));
    }

    public static JavaType resolveType(String topic, byte[] data, Headers headers){
        return switch(topic){
            case Topics.DATA_ACQUISITION_REQUESTED -> new ObjectMapper().constructType(DataAcquisitionRequested.class);
            case Topics.RESOURCE_ACQUIRED_ERROR -> new ObjectMapper().constructType(ResourceAcquired.class);
            case Topics.RESOURCE_NORMALIZED -> new ObjectMapper().constructType(ResourceNormalized.class);
            case Topics.RESOURCE_EVALUATED -> new ObjectMapper().constructType(ResourceEvaluated.class);
            case Topics.RESOURCE_NORMALIZED_ERROR -> new ObjectMapper().constructType(ResourceNormalized.class);
            case Topics.RESOURCE_NORMALIZED_RETRY -> new ObjectMapper().constructType(ResourceNormalized.class);
            case Topics.EVALUATION_REQUESTED -> new ObjectMapper().constructType(EvaluationRequested.class);
            case Topics.EVALUATION_REQUESTED_ERROR -> new ObjectMapper().constructType(EvaluationRequested.class);
            case Topics.EVALUATION_REQUESTED_RETRY -> new ObjectMapper().constructType(EvaluationRequested.class);
            default -> new ObjectMapper().constructType(Object.class);
        };
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

        //set the date format to the ISO 8601 ISO_INSTANT format to match other services
        SimpleDateFormat sdf = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ssX");
        objectMapper.setDateFormat(sdf);

        Map<Class<?>, Serializer<?>> serializers = Map.of(
                String.class, new StringSerializer(),
                ResourceEvaluated.Key.class, new JsonSerializer<>(objectMapper.constructType(ResourceEvaluated.Key.class), objectMapper),
                Object.class, new JsonSerializer<>(),
                byte[].class, new ByteArraySerializer()
        );
        return new DelegatingByTypeSerializer(serializers);
    }

    @Bean
    public Serializer<?> valueSerializer(ObjectMapper objectMapper) {
        Map<Class<?>, Serializer<?>> serializers = Map.of(
                ResourceAcquired.class, new JsonSerializer<>(objectMapper.constructType(ResourceAcquired.class), objectMapper).noTypeInfo(),
                ResourceNormalized.class, new JsonSerializer<>(objectMapper.constructType(ResourceNormalized.class), objectMapper).noTypeInfo(),
                DataAcquisitionRequested.class, new JsonSerializer<>(objectMapper.constructType(DataAcquisitionRequested.class), objectMapper).noTypeInfo(),
                ResourceEvaluated.class, new JsonSerializer<>(objectMapper.constructType(ResourceEvaluated.class), objectMapper).noTypeInfo(),
                AbstractResourceRecord.class, new JsonSerializer<>(objectMapper.constructType(AbstractResourceRecord.class), objectMapper).noTypeInfo(),
                EvaluationRequested.class, new JsonSerializer<>(objectMapper.constructType(EvaluationRequested.class), objectMapper).noTypeInfo(),
                String.class, new StringSerializer(),
                byte[].class, new ByteArraySerializer(),
                LinkedHashMap.class, new JsonSerializer<>(objectMapper.constructType(LinkedHashMap.class), objectMapper).noTypeInfo()
        );
        return new DelegatingByTypeSerializer(serializers);
    }

    @Bean
    public ProducerFactory<?, ?> producerFactory(
            KafkaProperties properties,
            ObjectProvider<SslBundles> sslBundles,
            Serializer<?> keySerializer,
            Serializer<?> valueSerializer) {
        Map<String, Object> producerProperties = properties.buildProducerProperties(sslBundles.getIfAvailable());
        producerProperties.put(ProducerConfig.INTERCEPTOR_CLASSES_CONFIG, TracingProducerInterceptor.class.getName());
        return  new DefaultKafkaProducerFactory<>(producerProperties, keySerializer, valueSerializer);
    }

    @Bean
    public RetryTopicConfiguration resourceNormalizedRetryTopic(@Qualifier("compressedKafkaTemplate")KafkaTemplate<String, ResourceNormalized> template) {
        return RetryTopicConfigurationBuilder
                .newInstance()
                .fixedBackOff(KafkaRetryConfig.getRetryBackoffMs())
                .maxAttempts(KafkaRetryConfig.getMaxAttempts())
                .concurrency(1)
                .includeTopic(Topics.RESOURCE_NORMALIZED)
                .retryTopicSuffix("-Retry")
                .dltSuffix("-Error")
                .notRetryOn(ValidationException.class).notRetryOn(FhirParseException.class).notRetryOn(MessageHandlingException.class)
                .useSingleTopicForSameIntervals()
                .doNotAutoCreateRetryTopics()
                .create(template);
    }

    @Bean
    public RetryTopicConfiguration evaluationRequestedRetryTopic(@Qualifier("compressedKafkaTemplate")KafkaTemplate<String, EvaluationRequested> template) {
        return RetryTopicConfigurationBuilder
                .newInstance()
                .fixedBackOff(KafkaRetryConfig.getRetryBackoffMs())
                .maxAttempts(KafkaRetryConfig.getMaxAttempts())
                .concurrency(1)
                .includeTopic(Topics.EVALUATION_REQUESTED)
                .retryTopicSuffix("-Retry")
                .dltSuffix("-Error")
                .notRetryOn(ValidationException.class).notRetryOn(FhirParseException.class).notRetryOn(MessageHandlingException.class)
                .useSingleTopicForSameIntervals()
                .doNotAutoCreateRetryTopics()
                .create(template);
    }

    @Bean
    public KafkaTemplate<?, ?> compressedKafkaTemplate(KafkaProperties properties,
                                                       ObjectProvider<SslBundles> sslBundles,
                                                       Serializer<?> keySerializer,
                                                       Serializer<?> valueSerializer) {
        properties.getProperties().put(ProducerConfig.COMPRESSION_TYPE_CONFIG, "zstd");
        return new KafkaTemplate<>(producerFactory(properties, sslBundles, keySerializer, valueSerializer));
    }
}
