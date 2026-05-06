package com.lantanagroup.link.measureeval.configs;

import com.fasterxml.jackson.databind.JavaType;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.measureeval.records.*;
import com.lantanagroup.link.measureeval.services.EvaluationRequestedConsumer;
import com.lantanagroup.link.measureeval.services.ResourcesNormalizedConsumer;
import com.lantanagroup.link.shared.kafka.AsyncListener;
import com.lantanagroup.link.shared.kafka.Properties;
import com.lantanagroup.link.shared.kafka.Topics;
import com.lantanagroup.link.shared.kafka.records.ResourceKey;
import io.opentelemetry.instrumentation.kafkaclients.v2_6.TracingConsumerInterceptor;
import io.opentelemetry.instrumentation.kafkaclients.v2_6.TracingProducerInterceptor;
import org.apache.kafka.clients.consumer.ConsumerConfig;
import org.apache.kafka.clients.producer.ProducerConfig;
import org.apache.kafka.common.TopicPartition;
import org.apache.kafka.common.header.Headers;
import org.apache.kafka.common.serialization.*;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.boot.autoconfigure.kafka.KafkaProperties;
import org.springframework.boot.ssl.SslBundles;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.kafka.config.ConcurrentKafkaListenerContainerFactory;
import org.springframework.kafka.core.*;
import org.springframework.kafka.listener.*;
import org.springframework.kafka.support.serializer.*;
import org.springframework.util.backoff.FixedBackOff;

import java.text.SimpleDateFormat;
import java.util.Collections;
import java.util.HashMap;
import java.util.LinkedHashMap;
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
    public DeadLetterPublishingRecoverer deadLetterPublishingRecoverer(KafkaTemplate<?, ?> compressedKafkaTemplate) {
        DeadLetterPublishingRecoverer recoverer = new DeadLetterPublishingRecoverer(compressedKafkaTemplate, (record, exception) ->
                new TopicPartition(record.topic() + "-Error", record.partition()));
        recoverer.setLogRecoveryRecord(true);
        return recoverer;
    }

    @Bean
    public DefaultErrorHandler defaultErrorHandler(ConsumerRecordRecoverer recoverer) {
        DefaultErrorHandler errorHandler = new DefaultErrorHandler(recoverer, new FixedBackOff(0L, 0L));
        errorHandler.setSeekAfterError(false);
        return errorHandler;
    }

    @Bean
    public Deserializer<?> keyDeserializer(ObjectMapper objectMapper) {
        Map<String, Deserializer<?>> deserializers = Map.of(
                Topics.RESOURCES_NORMALIZED, new JsonDeserializer<>(ResourceKey.class, objectMapper),
                Topics.RESOURCES_NORMALIZED_ERROR, new JsonDeserializer<>(ResourceKey.class, objectMapper),
                Topics.RESOURCES_NORMALIZED_RETRY, new JsonDeserializer<>(ResourceKey.class, objectMapper),
                Topics.EVALUATION_REQUESTED, new StringDeserializer(),
                Topics.EVALUATION_REQUESTED_ERROR, new StringDeserializer(),
                Topics.EVALUATION_REQUESTED_RETRY, new StringDeserializer());
        return new ErrorHandlingDeserializer<>(
                new DelegatingByTopicDeserializer(byPattern(deserializers), new StringDeserializer()));
    }

    @Bean
    public Deserializer<?> valueDeserializer(ObjectMapper objectMapper) {
        Map<String, Deserializer<?>> deserializers = Map.of(
                Topics.RESOURCES_NORMALIZED, new JsonDeserializer<>(ResourcesNormalized.class, objectMapper)
                        .trustedPackages("*")
                        .ignoreTypeHeaders()
                        .typeResolver(KafkaConfig::resolveType),
                Topics.RESOURCES_NORMALIZED_ERROR, new JsonDeserializer<>(ResourcesNormalized.class, objectMapper)
                        .trustedPackages("*")
                        .ignoreTypeHeaders()
                        .typeResolver(KafkaConfig::resolveType),
                Topics.MEASURE_REPORT_GENERATED, new JsonDeserializer<>(MeasureReportGenerated.class, objectMapper)
                        .trustedPackages("*")
                        .ignoreTypeHeaders()
                        .typeResolver(KafkaConfig::resolveType),
                Topics.RESOURCES_NORMALIZED_RETRY, new JsonDeserializer<>(ResourcesNormalized.class, objectMapper)
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

    public static JavaType resolveType(String topic, byte[] data, Headers headers) {
        return switch (topic) {
            case Topics.DATA_ACQUISITION_REQUESTED -> new ObjectMapper().constructType(DataAcquisitionRequested.class);
            case Topics.RESOURCES_NORMALIZED -> new ObjectMapper().constructType(ResourcesNormalized.class);
            case Topics.RESOURCES_NORMALIZED_ERROR -> new ObjectMapper().constructType(ResourcesNormalized.class);
            case Topics.RESOURCES_NORMALIZED_RETRY -> new ObjectMapper().constructType(ResourcesNormalized.class);
            case Topics.EVALUATION_REQUESTED -> new ObjectMapper().constructType(EvaluationRequested.class);
            case Topics.EVALUATION_REQUESTED_ERROR -> new ObjectMapper().constructType(EvaluationRequested.class);
            case Topics.EVALUATION_REQUESTED_RETRY -> new ObjectMapper().constructType(EvaluationRequested.class);
            case Topics.MEASURE_REPORT_GENERATED -> new ObjectMapper().constructType(MeasureReportGenerated.class);
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
                ResourceKey.class, new JsonSerializer<>(objectMapper.constructType(ResourceKey.class), objectMapper).noTypeInfo(),
                Object.class, new JsonSerializer<>(),
                byte[].class, new ByteArraySerializer()
        );
        return new DelegatingByTypeSerializer(serializers);
    }

    @Bean
    public Serializer<?> valueSerializer(ObjectMapper objectMapper) {
        Map<Class<?>, Serializer<?>> serializers = Map.of(
                ResourcesAcquired.class, new JsonSerializer<>(objectMapper.constructType(ResourcesAcquired.class), objectMapper).noTypeInfo(),
                ResourcesNormalized.class, new JsonSerializer<>(objectMapper.constructType(ResourcesNormalized.class), objectMapper).noTypeInfo(),
                DataAcquisitionRequested.class, new JsonSerializer<>(objectMapper.constructType(DataAcquisitionRequested.class), objectMapper).noTypeInfo(),
                MeasureReportGenerated.class, new JsonSerializer<>(objectMapper.constructType(MeasureReportGenerated.class), objectMapper).noTypeInfo(),
                AbstractResourceRecord.class, new JsonSerializer<>(objectMapper.constructType(AbstractResourceRecord.class), objectMapper).noTypeInfo(),
                EvaluationRequested.class, new JsonSerializer<>(objectMapper.constructType(EvaluationRequested.class), objectMapper).noTypeInfo(),
                String.class, new StringSerializer(),
                byte[].class, new ByteArraySerializer(),
                LinkedHashMap.class, new JsonSerializer<>(objectMapper.constructType(LinkedHashMap.class), objectMapper).noTypeInfo()
        );
        return new DelegatingByTypeSerializer(serializers);
    }


    @Bean
    public <K, V> ProducerFactory<K, V> producerFactoryWithOverrides(
            KafkaProperties properties,
            ObjectProvider<SslBundles> sslBundles,
            Serializer<K> keySerializer,
            Serializer<V> valueSerializer) {

        return producerFactoryWithOverrides(properties, sslBundles, keySerializer, valueSerializer, Collections.emptyMap());
    }


    public <K, V> ProducerFactory<K, V> producerFactoryWithOverrides(
            KafkaProperties properties,
            ObjectProvider<SslBundles> sslBundles,
            Serializer<K> keySerializer,
            Serializer<V> valueSerializer,
            Map<String, Object> customOverrides) {

        Map<String, Object> producerProperties = new HashMap<>(properties.buildProducerProperties(sslBundles.getIfAvailable()));
        producerProperties.putAll(customOverrides);
        producerProperties.put(ProducerConfig.INTERCEPTOR_CLASSES_CONFIG, TracingProducerInterceptor.class.getName());

        return new DefaultKafkaProducerFactory<>(producerProperties, keySerializer, valueSerializer);
    }


    @Bean
    public KafkaTemplate<?, ?> compressedKafkaTemplate(KafkaProperties properties,
                                                       ObjectProvider<SslBundles> sslBundles,
                                                       Serializer<?> keySerializer,
                                                       Serializer<?> valueSerializer) {

        Map<String, Object> overrides = new HashMap<>();
        overrides.put(ProducerConfig.COMPRESSION_TYPE_CONFIG,"zstd");
        return new KafkaTemplate<>(producerFactoryWithOverrides(properties, sslBundles, keySerializer, valueSerializer, overrides));
    }

    /*
        Added a new Kafka template so that:
        - it can be configured differently for max.block.ms (max amount of time Kafka producer will block when Kafka broker is unavailable) and number of retries.
        - avoids health check logic competing for resources with the real workload.
     */
    @Bean
    public KafkaTemplate<String, String> healthKafkaTemplate(KafkaProperties properties,
                                                             ObjectProvider<SslBundles> sslBundles,
                                                             Serializer<String> keySerializer,
                                                             Serializer<String> valueSerializer) {

        Map<String, Object> overrides = new HashMap<>();
        overrides.put(ProducerConfig.MAX_BLOCK_MS_CONFIG, Properties.MAX_BLOCK_MS_CONFIG);
        overrides.put(ProducerConfig.RETRIES_CONFIG, 0);

        return new KafkaTemplate<>(producerFactoryWithOverrides(properties, sslBundles, keySerializer, valueSerializer, overrides));
    }


    @Bean
    public ConcurrentMessageListenerContainer<String, EvaluationRequested> evaluationRequestedContainer(
            ConcurrentKafkaListenerContainerFactory<String, EvaluationRequested> factory,
            EvaluationRequestedConsumer consumer) {
        return getAsyncListenerContainer(factory, consumer, Topics.EVALUATION_REQUESTED);
    }

    @Bean
    public ConcurrentMessageListenerContainer<String, ResourcesNormalized> resourcesNormalizedContainer(
            ConcurrentKafkaListenerContainerFactory<String, ResourcesNormalized> factory,
            ResourcesNormalizedConsumer consumer) {
        return getAsyncListenerContainer(factory, consumer, Topics.RESOURCES_NORMALIZED);
    }

    private <K, V> ConcurrentMessageListenerContainer<K, V> getAsyncListenerContainer(
            ConcurrentKafkaListenerContainerFactory<K, V> factory,
            AsyncListener<?, ?> listener,
            String... topics) {
        ConcurrentMessageListenerContainer<K, V> container = factory.createContainer(topics);
        ContainerProperties containerProperties = container.getContainerProperties();
        containerProperties.setAckMode(ContainerProperties.AckMode.MANUAL_IMMEDIATE);
        containerProperties.setAsyncAcks(true);
        containerProperties.setMessageListener(listener);
        return container;
    }
}
