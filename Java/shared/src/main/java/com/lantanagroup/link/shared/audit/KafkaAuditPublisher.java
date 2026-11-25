package com.lantanagroup.link.shared.audit;

import com.lantanagroup.link.shared.kafka.Topics;
import org.apache.kafka.clients.producer.ProducerConfig;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.serialization.Serializer;
import org.apache.kafka.common.serialization.StringSerializer;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.DisposableBean;
import org.springframework.kafka.core.DefaultKafkaProducerFactory;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.core.ProducerFactory;
import org.springframework.kafka.support.ProducerListener;
import org.springframework.kafka.support.SendResult;
import org.springframework.kafka.support.serializer.JsonSerializer;

import java.time.Duration;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.function.LongSupplier;
import java.util.function.Supplier;

/**
 * Publishes audit events to {@code AuditableEventOccurred} on a private producer, so a service's own Kafka
 * settings for serialisation, retries and acknowledgement cannot change how an audit event is delivered.
 * <p>
 * Each {@link #publish} waits for the broker's confirmation for at most the response timeout, one budget
 * covering both the send and the wait. An event not confirmed in time is settled later by a completion
 * callback. A failure is counted and logged with the whole event, exactly once, and never thrown.
 */
public final class KafkaAuditPublisher implements AuditPublisher, DisposableBean {
    private static final Logger logger = LoggerFactory.getLogger(KafkaAuditPublisher.class);
    private static final String CLIENT_ID_SUFFIX = "audit";
    private static final String CLOSED = "The audit publisher is closed";

    private final Supplier<ProducerFactory<String, AuditEventMessage>> factorySupplier;
    private final long responseTimeoutNanos;
    private final AuditMetrics metrics;
    private final LongSupplier nanoTime;

    private final Object lock = new Object();
    private volatile ProducerFactory<String, AuditEventMessage> producerFactory;
    private volatile KafkaTemplate<String, AuditEventMessage> template;
    private volatile boolean closed;

    public KafkaAuditPublisher(Supplier<ProducerFactory<String, AuditEventMessage>> factorySupplier,
                               Duration responseTimeout, AuditMetrics metrics) {
        this(factorySupplier, responseTimeout, metrics, System::nanoTime);
    }

    KafkaAuditPublisher(Supplier<ProducerFactory<String, AuditEventMessage>> factorySupplier, Duration responseTimeout,
                        AuditMetrics metrics, LongSupplier nanoTime) {
        this.factorySupplier = factorySupplier;
        this.responseTimeoutNanos = responseTimeout.toNanos();
        this.metrics = metrics;
        this.nanoTime = nanoTime;
    }

    /**
     * The audit producer's configuration: a copy of the service's producer configuration, so it connects and
     * authenticates as the service's own producers do, with the settings audit delivery depends on stated
     * explicitly. Inherited serialisers, {@code retries} and {@code max.in.flight.requests.per.connection} are
     * dropped, so nothing inherited can make the idempotent producer invalid. An inherited
     * {@code transactional.id} is dropped too: it would make the factory transactional, and an audit event is
     * sent outside any transaction, which such a factory refuses. The given map is not changed.
     */
    public static Map<String, Object> producerConfig(Map<String, Object> serviceProducerConfig, Duration responseTimeout) {
        Map<String, Object> config = serviceProducerConfig == null
                ? new HashMap<>()
                : new HashMap<>(serviceProducerConfig);
        config.remove(ProducerConfig.KEY_SERIALIZER_CLASS_CONFIG);
        config.remove(ProducerConfig.VALUE_SERIALIZER_CLASS_CONFIG);
        config.remove(ProducerConfig.RETRIES_CONFIG);
        config.remove(ProducerConfig.MAX_IN_FLIGHT_REQUESTS_PER_CONNECTION);
        config.remove(ProducerConfig.TRANSACTIONAL_ID_CONFIG);

        config.put(ProducerConfig.MAX_BLOCK_MS_CONFIG, responseTimeout.toMillis());
        config.put(ProducerConfig.ENABLE_IDEMPOTENCE_CONFIG, true);
        config.put(ProducerConfig.ACKS_CONFIG, "all");

        Object clientId = config.get(ProducerConfig.CLIENT_ID_CONFIG);
        String inherited = clientId == null ? "" : clientId.toString();
        config.put(ProducerConfig.CLIENT_ID_CONFIG,
                inherited.isBlank() ? CLIENT_ID_SUFFIX : inherited + "-" + CLIENT_ID_SUFFIX);
        return config;
    }

    /**
     * A producer factory on the given configuration, with the audit key and value serialisers. The serialisers
     * are set up in code and the factory is told not to configure them from the properties: a service's own
     * {@code spring.json.*} settings, copied with the rest, would otherwise be applied to the audit serialiser,
     * which refuses to be configured both ways and so would fail every send.
     */
    public static ProducerFactory<String, AuditEventMessage> producerFactory(Map<String, Object> config) {
        return new DefaultKafkaProducerFactory<>(config, new StringSerializer(), valueSerializer(), false);
    }

    /** The audit value serialiser: the wire mapper, with no type-info headers. */
    static Serializer<AuditEventMessage> valueSerializer() {
        return new JsonSerializer<AuditEventMessage>(AuditJson.wireMapper()).noTypeInfo();
    }

    @Override
    public void publish(AuditEventMessage event) {
        AtomicBoolean settled = new AtomicBoolean();
        try {
            long deadline = nanoTime.getAsLong() + responseTimeoutNanos;
            KafkaTemplate<String, AuditEventMessage> kafkaTemplate = template();
            String key = event.facilityId() == null ? "" : event.facilityId();

            CompletableFuture<SendResult<String, AuditEventMessage>> future =
                    kafkaTemplate.send(new ProducerRecord<>(Topics.AUDITABLE_EVENT_OCCURRED, key, event));
            future.whenComplete((result, failure) -> {
                if (failure != null) {
                    settleFailure(event, failure, settled);
                }
            });

            awaitConfirmation(event, future, deadline - nanoTime.getAsLong());
        } catch (RuntimeException e) {
            settleFailure(event, e, settled);
        }
    }

    private void awaitConfirmation(AuditEventMessage event, CompletableFuture<?> future, long remainingNanos) {
        try {
            if (remainingNanos <= 0) {
                if (!future.isDone()) {
                    warnNotConfirmed(event);
                }
                return;
            }
            future.get(remainingNanos, TimeUnit.NANOSECONDS);
        } catch (TimeoutException e) {
            warnNotConfirmed(event);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        } catch (ExecutionException e) {
            // The completion callback settles this failure, possibly just after publish returns.
        }
    }

    private void warnNotConfirmed(AuditEventMessage event) {
        logger.warn("Audit event {} {} was not confirmed by Kafka within {} ms; delivery continues in the background",
                action(event), event.resource(), TimeUnit.NANOSECONDS.toMillis(responseTimeoutNanos));
    }

    private static String action(AuditEventMessage event) {
        return event == null || event.action() == null ? null : event.action().wireName();
    }

    private void settleFailure(AuditEventMessage event, Throwable failure, AtomicBoolean settled) {
        if (!settled.compareAndSet(false, true)) {
            return;
        }
        try {
            metrics.publishFailed(event == null ? null : event.resource());
            logger.error("Audit event {} {} could not be published ({}): {}",
                    action(event), event == null ? null : event.resource(),
                    AuditJson.describe(failure), AuditJson.logLine(event));
            // Where it was thrown stands in for the stack trace: the exception is never handed to the logger,
            // whose output would carry its message as it stands.
            logger.debug("Audit publish failure was thrown at {}", AuditJson.thrownAt(failure));
        } catch (RuntimeException e) {
            // Nothing in the audit path may throw into a request, not even a failing logger.
        }
    }

    /** The template, created on first use. Throws once the publisher is closed, so nothing is built or sent. */
    private KafkaTemplate<String, AuditEventMessage> template() {
        if (closed) {
            throw new IllegalStateException(CLOSED);
        }
        KafkaTemplate<String, AuditEventMessage> current = template;
        if (current != null) {
            return current;
        }
        synchronized (lock) {
            if (closed) {
                throw new IllegalStateException(CLOSED);
            }
            if (template == null) {
                ProducerFactory<String, AuditEventMessage> factory = factorySupplier.get();
                KafkaTemplate<String, AuditEventMessage> created = new KafkaTemplate<>(factory);
                // The default LoggingProducerListener would log every failure a second time, with the
                // record's toString; this publisher logs it once, as the escaped wire JSON.
                created.setProducerListener(new ProducerListener<>() {
                });
                template = created;
                producerFactory = factory;
            }
            return template;
        }
    }

    boolean producerFactoryCreated() {
        return producerFactory != null;
    }

    /**
     * Destroys the producer factory, closing its producer, if one was ever created. A later {@link #publish}
     * sends nothing and is counted and logged as a failure.
     */
    @Override
    public void destroy() {
        synchronized (lock) {
            closed = true;
            ProducerFactory<String, AuditEventMessage> factory = producerFactory;
            if (factory instanceof DisposableBean disposable) {
                try {
                    disposable.destroy();
                } catch (Exception e) {
                    logger.warn("Could not close the audit Kafka producer: {}", AuditJson.describe(e));
                }
            }
        }
    }
}
