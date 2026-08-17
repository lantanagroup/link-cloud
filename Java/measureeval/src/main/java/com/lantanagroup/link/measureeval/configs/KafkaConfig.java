package com.lantanagroup.link.measureeval.configs;

import ca.uhn.fhir.parser.DataFormatException;
import com.fasterxml.jackson.databind.JavaType;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.measureeval.records.*;
import com.lantanagroup.link.shared.kafka.RetryTopicRecoverer;
import com.lantanagroup.link.shared.kafka.RetryTopicRecovererFactory;
import com.lantanagroup.link.shared.config.KafkaRetryConfig;
import com.lantanagroup.link.shared.exceptions.FhirParseException;
import com.lantanagroup.link.shared.exceptions.ValidationException;
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
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.boot.autoconfigure.kafka.KafkaProperties;
import org.springframework.boot.ssl.SslBundles;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.kafka.config.ConcurrentKafkaListenerContainerFactory;
import org.springframework.kafka.core.*;
import org.springframework.kafka.listener.*;
import org.springframework.kafka.retrytopic.RetryTopicConfiguration;
import org.springframework.kafka.retrytopic.RetryTopicConfigurationBuilder;
import org.springframework.kafka.retrytopic.RetryTopicHeaders;
import org.springframework.kafka.support.serializer.*;
import org.springframework.messaging.MessageHandlingException;
import org.springframework.util.backoff.FixedBackOff;

import java.text.SimpleDateFormat;
import java.util.Collections;
import java.util.HashMap;
import java.util.LinkedHashMap;
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

    // Delay + termination are driven by the custom recoverer; this config only provisions
    // the backoff-aware retry listener. Do not rely on Spring's own retry/DLT routing here.
    //
    // Both retried topics (ResourcesNormalized and EvaluationRequested) share one
    // RetryTopicConfiguration. Spring Kafka 3.1.x does not reliably bootstrap multiple
    // RetryTopicConfiguration beans — only one listener's containers get provisioned and the
    // other @KafkaListener is silently dropped (no error). A single config with includeTopics
    // covers both deterministically. The settings are identical across the two topics, so there
    // is no behavioral difference vs. the previous per-topic beans.
    @Bean
    @ConditionalOnProperty(prefix = "spring.kafka.retry", name = "disable-retry-consumer", havingValue = "false", matchIfMissing = true)
    public RetryTopicConfiguration measureEvalRetryTopics(@Qualifier("compressedKafkaTemplate") KafkaTemplate<String, Object> template) {
        return RetryTopicConfigurationBuilder
                .newInstance()
                .concurrency(1)
                .includeTopics(List.of(Topics.RESOURCES_NORMALIZED, Topics.EVALUATION_REQUESTED))
                .retryTopicSuffix("-Retry")
                .dltSuffix("-Error")
                // Container-thread poison (malformed payload / deserialization) never succeeds on retry,
                // so route it straight to -Error. Mirrors the NON_RETRYABLE set (RetryTopicRecovererFactory)
                // used on the async path.
                .notRetryOn(DeserializationException.class)
                .useSingleTopicForSameIntervals()
                .doNotAutoCreateRetryTopics()
                // Keep the DLT in the destination chain — it is the routing target for container-thread
                // poison (see notRetryOn above), so removing it with doNotConfigureDlt() would leave a
                // malformed record with no destination and drop it instead of preserving it.
                // Only suppress its *listener*: nothing consumes -Error (there is no @DltHandler), so the
                // container it would otherwise start just re-reads each dead letter, fails again on the
                // same bytes, and commits the offset — which both doubles the error logging for poison
                // and advances the group past records that dead-letter replay will need to re-read.
                .autoStartDltHandler(false)
                .create(template);
    }

    /**
     * Exceptions that can never succeed on retry (malformed content / deserialization); routed
     * straight to the error topic. Supplied to the shared {@link RetryTopicRecovererFactory} — the
     * FHIR/HAPI types live on this module's classpath, not in shared.
     *
     * <p>Package-private (not {@code private}) so {@code KafkaConfigTest} exercises the resolver and
     * classifier against this exact production set rather than a copy that can silently drift.</p>
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
    public ConsumerRecordRecoverer resourceNormalizedRecoverer(
            @Qualifier("compressedKafkaTemplate")
            KafkaTemplate<String, ResourcesNormalized> kafkaTemplate,
            KafkaRetryConfig retryConfig) {

        return createRetryTopicRecoverer(
                kafkaTemplate,
                "ResourcesNormalized-Retry",
                "ResourcesNormalized-Error",
                retryConfig
        );
    }

    @Bean
    public ConsumerRecordRecoverer evaluationRequestedRecoverer(
            @Qualifier("compressedKafkaTemplate")
            KafkaTemplate<String, EvaluationRequested> kafkaTemplate,
            KafkaRetryConfig retryConfig) {

        return createRetryTopicRecoverer(
                kafkaTemplate,
                "EvaluationRequested-Retry",
                "EvaluationRequested-Error",
                retryConfig
        );
    }

    @Bean
    public ConcurrentKafkaListenerContainerFactory<String, Object> manualAckListenerContainerFactory(
            ConsumerFactory<String, Object> consumerFactory,
            @Qualifier("defaultErrorHandler") CommonErrorHandler errorHandler) {

        ConcurrentKafkaListenerContainerFactory<String, Object> factory =
                new ConcurrentKafkaListenerContainerFactory<>();
        factory.setConsumerFactory(consumerFactory);
        factory.getContainerProperties().setAckMode(ContainerProperties.AckMode.MANUAL_IMMEDIATE);
        factory.getContainerProperties().setAsyncAcks(true);
        factory.setCommonErrorHandler(errorHandler);
        return factory;
    }

}
