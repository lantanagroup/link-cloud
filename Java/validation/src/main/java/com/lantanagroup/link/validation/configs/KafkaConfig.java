package com.lantanagroup.link.validation.configs;

import ca.uhn.fhir.parser.DataFormatException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.shared.config.KafkaRetryConfig;
import com.lantanagroup.link.shared.exceptions.FhirParseException;
import com.lantanagroup.link.shared.exceptions.ValidationException;
import com.lantanagroup.link.shared.kafka.Properties;
import com.lantanagroup.link.shared.kafka.RetryTopicRecoverer;
import com.lantanagroup.link.shared.kafka.RetryTopicRecovererFactory;
import com.lantanagroup.link.shared.kafka.Topics;
import com.lantanagroup.link.validation.records.ReadyForValidation;
import com.lantanagroup.link.validation.records.ValidationComplete;
import io.opentelemetry.instrumentation.kafkaclients.v2_6.TracingConsumerInterceptor;
import io.opentelemetry.instrumentation.kafkaclients.v2_6.TracingProducerInterceptor;
import org.apache.kafka.clients.consumer.ConsumerConfig;
import org.apache.kafka.clients.producer.ProducerConfig;
import org.apache.kafka.common.TopicPartition;
import org.apache.kafka.common.serialization.*;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.boot.autoconfigure.kafka.KafkaProperties;
import org.springframework.boot.ssl.SslBundles;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.kafka.config.ConcurrentKafkaListenerContainerFactory;
import org.springframework.kafka.core.*;
import org.springframework.kafka.listener.*;
import org.springframework.kafka.retrytopic.RetryTopicConfiguration;
import org.springframework.kafka.retrytopic.RetryTopicConfigurationBuilder;
import org.springframework.kafka.support.serializer.*;
import org.springframework.messaging.MessageHandlingException;
import org.springframework.util.backoff.FixedBackOff;

import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
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
    public DeadLetterPublishingRecoverer deadLetterPublishingRecoverer(KafkaTemplate<?, ?> compressedKafkaTemplate) {
        DeadLetterPublishingRecoverer recoverer = new DeadLetterPublishingRecoverer(compressedKafkaTemplate, (record, exception) ->
                new TopicPartition(record.topic() + "-Error", record.partition()));
        recoverer.setLogRecoveryRecord(true);
        return recoverer;
    }

    @Bean
    public DefaultErrorHandler defaultErrorHandler(@Qualifier("deadLetterPublishingRecoverer") ConsumerRecordRecoverer recoverer) {
        DefaultErrorHandler errorHandler = new DefaultErrorHandler(recoverer, new FixedBackOff(0L, 0L));
        errorHandler.setSeekAfterError(false);
        return errorHandler;
    }

    @Bean
    public Deserializer<?> keyDeserializer(ObjectMapper objectMapper) {
        Map<String, Deserializer<?>> deserializers = Map.of(
                Topics.READY_FOR_VALIDATION, new JsonDeserializer<>(ReadyForValidation.Key.class, objectMapper),
                Topics.READY_FOR_VALIDATION_RETRY, new JsonDeserializer<>(ReadyForValidation.Key.class, objectMapper),
                Topics.READY_FOR_VALIDATION_ERROR, new JsonDeserializer<>(ReadyForValidation.Key.class, objectMapper));
        return new ErrorHandlingDeserializer<>(
                new DelegatingByTopicDeserializer(byPattern(deserializers), new VoidDeserializer()));
    }

    @Bean
    public Deserializer<?> valueDeserializer(ObjectMapper objectMapper) {
        Map<String, Deserializer<?>> deserializers = Map.of(
                Topics.READY_FOR_VALIDATION, new JsonDeserializer<>(ReadyForValidation.class, objectMapper),
                Topics.READY_FOR_VALIDATION_RETRY, new JsonDeserializer<>(ReadyForValidation.class, objectMapper),
                Topics.READY_FOR_VALIDATION_ERROR, new JsonDeserializer<>(ReadyForValidation.class, objectMapper));
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
        Map<Class<?>, Serializer<?>> serializers = Map.of(
                String.class, new StringSerializer(),
                ReadyForValidation.Key.class, getJsonSerializer(objectMapper, ReadyForValidation.Key.class)
        );
        return new DelegatingByTypeSerializer(serializers);
    }

    @Bean
    public Serializer<?> valueSerializer(ObjectMapper objectMapper) {
        Map<Class<?>, Serializer<?>> serializers = Map.of(
                String.class, new StringSerializer(),
                ValidationComplete.class, getJsonSerializer(objectMapper, ValidationComplete.class),
                ReadyForValidation.class, getJsonSerializer(objectMapper, ReadyForValidation.class)
        );
        return new DelegatingByTypeSerializer(serializers);
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
        Map<String, Object> producerProperties = new HashMap<>(properties.buildProducerProperties(sslBundles.getIfAvailable()));
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
    public KafkaTemplate<?, ?> compressedKafkaTemplate(
            KafkaProperties properties,
            ObjectProvider<SslBundles> sslBundles,
            Serializer<?> keySerializer,
            Serializer<?> valueSerializer) {
        Map<String, Object> overrides = new HashMap<>();
        overrides.put(ProducerConfig.COMPRESSION_TYPE_CONFIG, "zstd");
        return new KafkaTemplate<>(getProducerFactory(properties, sslBundles, keySerializer, valueSerializer, overrides));
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

    // ============================== Retry Infrastructure ==============================

    /**
     * Exceptions that can never succeed on retry (malformed content / deserialization); routed
     * straight to the error topic.
     */
    static final Set<Class<? extends Throwable>> NON_RETRYABLE = Set.of(
            FhirParseException.class,
            ValidationException.class,
            MessageHandlingException.class,
            DataFormatException.class,
            DeserializationException.class);

    private RetryTopicRecoverer createRetryTopicRecoverer(
            KafkaTemplate<?, ?> kafkaTemplate,
            String retryTopic,
            String errorTopic,
            KafkaRetryConfig retryConfig) {
        return RetryTopicRecovererFactory.create(kafkaTemplate, retryTopic, errorTopic, retryConfig, NON_RETRYABLE);
    }

    @Bean
    public ConsumerRecordRecoverer readyForValidationRecoverer(
            @Qualifier("compressedKafkaTemplate")
            KafkaTemplate<?, ?> kafkaTemplate,
            KafkaRetryConfig retryConfig) {

        return createRetryTopicRecoverer(
                kafkaTemplate,
                Topics.READY_FOR_VALIDATION_RETRY,
                Topics.READY_FOR_VALIDATION_ERROR,
                retryConfig
        );
    }

    @Bean
    @ConditionalOnProperty(prefix = "spring.kafka.retry", name = "disable-retry-consumer", havingValue = "false", matchIfMissing = true)
    public RetryTopicConfiguration validationRetryTopics(@Qualifier("compressedKafkaTemplate") KafkaTemplate<String, Object> template) {
        return RetryTopicConfigurationBuilder
                .newInstance()
                .concurrency(1)
                .includeTopics(List.of(Topics.READY_FOR_VALIDATION))
                .retryTopicSuffix("-Retry")
                .dltSuffix("-Error")
                .notRetryOn(DeserializationException.class)
                .useSingleTopicForSameIntervals()
                .doNotAutoCreateRetryTopics()
                .create(template);
    }

    @Bean
    public ConcurrentKafkaListenerContainerFactory<Object, Object> manualAckListenerContainerFactory(
            ConsumerFactory<Object, Object> consumerFactory,
            @Qualifier("defaultErrorHandler") CommonErrorHandler errorHandler) {

        ConcurrentKafkaListenerContainerFactory<Object, Object> factory =
                new ConcurrentKafkaListenerContainerFactory<>();
        factory.setConsumerFactory(consumerFactory);
        factory.getContainerProperties().setAckMode(ContainerProperties.AckMode.MANUAL_IMMEDIATE);
        factory.getContainerProperties().setAsyncAcks(true);
        factory.setCommonErrorHandler(errorHandler);
        return factory;
    }
}
