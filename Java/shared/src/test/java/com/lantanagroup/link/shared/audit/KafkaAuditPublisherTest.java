package com.lantanagroup.link.shared.audit;

import ch.qos.logback.classic.Level;
import ch.qos.logback.classic.Logger;
import ch.qos.logback.classic.spi.ILoggingEvent;
import ch.qos.logback.core.read.ListAppender;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.shared.kafka.Topics;
import org.apache.kafka.clients.producer.Callback;
import org.apache.kafka.clients.producer.MockProducer;
import org.apache.kafka.clients.producer.Producer;
import org.apache.kafka.clients.producer.ProducerConfig;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.clients.producer.RecordMetadata;
import org.apache.kafka.common.errors.TimeoutException;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.apache.kafka.common.serialization.StringSerializer;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.DisposableBean;
import org.springframework.kafka.core.DefaultKafkaProducerFactory;
import org.springframework.kafka.core.ProducerFactory;
import org.springframework.kafka.support.serializer.JsonSerializer;

import java.io.IOException;
import java.io.InputStream;
import java.time.Duration;
import java.time.Instant;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.Future;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicLong;

class KafkaAuditPublisherTest {
    private static final Instant EVENT_DATE = Instant.parse("2026-11-23T15:04:05.123Z");
    private static final Duration FIVE_SECONDS = Duration.ofSeconds(5);

    private final RecordedCounter counter = new RecordedCounter();
    private final AuditMetrics metrics = new AuditMetrics(counter.openTelemetry(), "Validation");

    private final Logger publisherLogger = (Logger) LoggerFactory.getLogger(KafkaAuditPublisher.class);
    private final ListAppender<ILoggingEvent> logs = new ListAppender<>();

    // A fake nano source: time moves only when a test moves it.
    private final AtomicLong now = new AtomicLong();

    @BeforeEach
    void captureLogs() {
        logs.start();
        publisherLogger.addAppender(logs);
        publisherLogger.setAdditive(false);
    }

    @AfterEach
    void releaseLogs() {
        publisherLogger.detachAppender(logs);
        publisherLogger.setAdditive(true);
        Thread.interrupted();
    }

    @Test
    void publish_sendsToTheAuditTopic_keyedByFacility() {
        TestProducer producer = new TestProducer(true);
        publisher(new TestFactory(producer), FIVE_SECONDS).publish(updateEvent());

        Assertions.assertEquals(1, producer.history().size());
        ProducerRecord<String, AuditEventMessage> record = producer.history().get(0);
        Assertions.assertEquals(Topics.AUDITABLE_EVENT_OCCURRED, record.topic());
        Assertions.assertEquals("Hospital1", record.key());
    }

    @Test
    void publish_noFacility_keyIsEmptyString() {
        TestProducer producer = new TestProducer(true);
        publisher(new TestFactory(producer), FIVE_SECONDS).publish(createEvent());

        Assertions.assertEquals("", producer.history().get(0).key());
    }

    @Test
    void publish_valueSerialises_toTheContractJson() throws IOException {
        TestProducer producer = new TestProducer(true);
        publisher(new TestFactory(producer), FIVE_SECONDS).publish(updateEvent());

        ProducerRecord<String, AuditEventMessage> record = producer.history().get(0);
        RecordHeaders headers = new RecordHeaders();
        byte[] bytes = KafkaAuditPublisher.valueSerializer().serialize(record.topic(), headers, record.value());

        ObjectMapper plain = new ObjectMapper();
        Assertions.assertEquals(readFixture(plain, "audit-event-update.json"), plain.readTree(bytes));
        Assertions.assertNull(headers.lastHeader("__TypeId__"));
        Assertions.assertNull(record.headers().lastHeader("__TypeId__"));
    }

    @Test
    void confirmedWithinBudget_countsNothing() {
        TestProducer producer = new TestProducer(true);
        KafkaAuditPublisher publisher = publisher(new TestFactory(producer), FIVE_SECONDS);

        long elapsed = timeMillis(() -> publisher.publish(updateEvent()));

        Assertions.assertTrue(elapsed < 1_000, "publish took " + elapsed + " ms");
        Assertions.assertEquals(0L, failedCount());
        Assertions.assertTrue(logs.list.isEmpty());
    }

    @Test
    void notConfirmedWithinBudget_returnsAndCountsNothingYet() {
        TestProducer producer = new TestProducer(false);
        KafkaAuditPublisher publisher = publisher(new TestFactory(producer), Duration.ofMillis(200));

        long elapsed = timeMillis(() -> publisher.publish(updateEvent()));

        Assertions.assertTrue(elapsed < 5_000, "publish took " + elapsed + " ms");
        Assertions.assertEquals(0L, failedCount());
        Assertions.assertEquals(1, logs.list.size());
        ILoggingEvent warning = logs.list.get(0);
        Assertions.assertEquals(Level.WARN, warning.getLevel());
        Assertions.assertTrue(warning.getFormattedMessage().contains("Update"), warning.getFormattedMessage());
        Assertions.assertTrue(warning.getFormattedMessage().contains("Category"), warning.getFormattedMessage());
        Assertions.assertTrue(warning.getFormattedMessage().contains("background"), warning.getFormattedMessage());
    }

    @Test
    void sendThatBlocksPartOfTheBudget_waitsOnlyTheRemainder() {
        TestProducer producer = new TestProducer(false);
        producer.onSend = () -> now.addAndGet(Duration.ofMillis(4_900).toNanos());
        KafkaAuditPublisher publisher = publisher(new TestFactory(producer), FIVE_SECONDS);

        long elapsed = timeMillis(() -> publisher.publish(updateEvent()));

        Assertions.assertTrue(elapsed < 2_000, "publish took " + elapsed + " ms");
        Assertions.assertEquals(0L, failedCount());
        Assertions.assertEquals(Level.WARN, logs.list.get(0).getLevel());
    }

    @Test
    void sendThatUsesTheWholeBudget_doesNotWait() {
        TestProducer producer = new TestProducer(false);
        producer.onSend = () -> now.addAndGet(Duration.ofSeconds(6).toNanos());
        KafkaAuditPublisher publisher = publisher(new TestFactory(producer), FIVE_SECONDS);

        long elapsed = timeMillis(() -> publisher.publish(updateEvent()));

        Assertions.assertTrue(elapsed < 2_000, "publish took " + elapsed + " ms");
        Assertions.assertEquals(0L, failedCount());
        Assertions.assertEquals(Level.WARN, logs.list.get(0).getLevel());
    }

    @Test
    void lateFailure_isCountedOnceAndLogsTheWholeEvent() {
        TestProducer producer = new TestProducer(false);
        AuditEventMessage event = updateEvent();
        publisher(new TestFactory(producer), Duration.ofMillis(50)).publish(event);
        Assertions.assertEquals(0L, failedCount());

        Assertions.assertTrue(producer.errorNext(new TimeoutException("Expiring 1 record(s)")));

        Assertions.assertEquals(1L, failedCount());
        Assertions.assertEquals(List.of("Category"), counter.resources());
        List<ILoggingEvent> errors = errors();
        Assertions.assertEquals(1, errors.size());
        String message = errors.get(0).getFormattedMessage();
        Assertions.assertTrue(message.contains(AuditJson.logLine(event)), message);
        Assertions.assertTrue(message.contains("Update"), message);
        Assertions.assertTrue(message.contains("Category"), message);
        // Spring Kafka's KafkaProducerException says only "Failed to send"; the cause says why.
        Assertions.assertTrue(message.contains("caused by TimeoutException: Expiring 1 record(s)"), message);
    }

    @Test
    void failureText_isEscaped() {
        TestProducer producer = new TestProducer(false);
        publisher(new TestFactory(producer), Duration.ofMillis(50)).publish(updateEvent());

        Assertions.assertTrue(producer.errorNext(
                new TimeoutException("line\nbreak, \u001b[31mred,  separator, \u0085next, \u007fdelete")));

        List<ILoggingEvent> errors = errors();
        Assertions.assertEquals(1, errors.size());
        String message = errors.get(0).getFormattedMessage();
        for (char raw : new char[]{'\n', '\r', '\u001b', ' ', '\u0085', '\u007f'}) {
            Assertions.assertEquals(-1, message.indexOf(raw), "raw U+" + Integer.toHexString(raw) + " in " + message);
        }
        Assertions.assertTrue(message.contains("line\\nbreak"), message);
        Assertions.assertTrue(message.toLowerCase().contains("\\u001b[31mred"), message);
        Assertions.assertTrue(message.toLowerCase().contains("\\u2028separator"), message);
    }

    @Test
    void lateSuccess_countsNothing() {
        TestProducer producer = new TestProducer(false);
        publisher(new TestFactory(producer), Duration.ofMillis(50)).publish(updateEvent());
        int logged = logs.list.size();

        Assertions.assertTrue(producer.completeNext());

        Assertions.assertEquals(0L, failedCount());
        Assertions.assertEquals(logged, logs.list.size());
    }

    @Test
    void failureBeforeTheWait_isCountedOnce() {
        TestProducer producer = new TestProducer(false);
        // The first read of the clock after the send fails the record, so it is already failed when the
        // publisher starts to wait: the callback and the ExecutionException from the wait both see it.
        KafkaAuditPublisher publisher = new KafkaAuditPublisher(() -> new TestFactory(producer), FIVE_SECONDS,
                metrics, () -> {
            if (!producer.history().isEmpty() && producer.failed.compareAndSet(0, 1)) {
                producer.errorNext(new TimeoutException("failed"));
            }
            return now.get();
        });

        long elapsed = timeMillis(() -> publisher.publish(updateEvent()));

        Assertions.assertEquals(1, producer.failed.get());
        Assertions.assertTrue(elapsed < 1_000, "publish took " + elapsed + " ms");
        Assertions.assertEquals(1L, failedCount());
        Assertions.assertEquals(1, errors().size());
    }

    @Test
    void sendThatThrows_isCountedOnce_andNothingPropagates() {
        TestProducer producer = new TestProducer(false);
        producer.onSend = () -> {
            throw new TimeoutException("Topic AuditableEventOccurred not present\r\nin metadata after 3000 ms.");
        };
        KafkaAuditPublisher publisher = publisher(new TestFactory(producer), FIVE_SECONDS);

        Assertions.assertDoesNotThrow(() -> publisher.publish(updateEvent()));

        Assertions.assertEquals(1L, failedCount());
        List<ILoggingEvent> errors = errors();
        Assertions.assertEquals(1, errors.size());
        String message = errors.get(0).getFormattedMessage();
        Assertions.assertTrue(message.contains("not present\\r\\nin metadata"), message);
        Assertions.assertFalse(message.contains("\n") || message.contains("\r"), "the ERROR is one line");
    }

    @Test
    void supplierThatThrows_isCounted_andRetriedNextTime() {
        TestProducer producer = new TestProducer(true);
        AtomicInteger calls = new AtomicInteger();
        KafkaAuditPublisher publisher = new KafkaAuditPublisher(() -> {
            if (calls.incrementAndGet() == 1) {
                throw new IllegalStateException("no bootstrap servers");
            }
            return new TestFactory(producer);
        }, FIVE_SECONDS, metrics, now::get);

        Assertions.assertDoesNotThrow(() -> publisher.publish(updateEvent()));
        Assertions.assertFalse(publisher.producerFactoryCreated());
        Assertions.assertEquals(1L, failedCount());

        publisher.publish(updateEvent());

        Assertions.assertEquals(2, calls.get());
        Assertions.assertTrue(publisher.producerFactoryCreated());
        Assertions.assertEquals(1, producer.history().size());
        Assertions.assertEquals(1L, failedCount());

        publisher.publish(updateEvent());
        Assertions.assertEquals(2, calls.get(), "the factory is obtained once");
    }

    @Test
    void interruptedWhileWaiting_restoresTheFlag() {
        TestProducer producer = new TestProducer(false);
        KafkaAuditPublisher publisher = publisher(new TestFactory(producer), FIVE_SECONDS);

        Thread.currentThread().interrupt();
        long elapsed = timeMillis(() -> publisher.publish(updateEvent()));

        Assertions.assertTrue(Thread.interrupted(), "the interrupt flag is restored");
        Assertions.assertTrue(elapsed < 2_000, "publish took " + elapsed + " ms");
        Assertions.assertEquals(0L, failedCount());
    }

    /** No level of the log is handed the exception, whose message would reach it as it stands. */
    @Test
    void failedSend_handsNoExceptionToTheLogger_atAnyLevel() {
        Level previous = publisherLogger.getLevel();
        publisherLogger.setLevel(Level.DEBUG);
        try {
            TestProducer producer = new TestProducer(false);
            producer.onSend = () -> {
                throw new TimeoutException("Topic not present\r\n2026-11-25 ERROR forged entry");
            };

            publisher(new TestFactory(producer), FIVE_SECONDS).publish(updateEvent());

            Assertions.assertEquals(1, errors().size());
            List<ILoggingEvent> debug = logs.list.stream().filter(e -> e.getLevel() == Level.DEBUG).toList();
            Assertions.assertEquals(1, debug.size());
            Assertions.assertTrue(debug.get(0).getFormattedMessage().contains("thrown at "),
                    debug.get(0).getFormattedMessage());
            for (ILoggingEvent logged : logs.list) {
                String message = logged.getFormattedMessage();
                Assertions.assertNull(logged.getThrowableProxy(), message);
                Assertions.assertFalse(message.contains("\n") || message.contains("\r"), message);
            }
        } finally {
            publisherLogger.setLevel(previous);
        }
    }

    @Test
    void destroyThatThrows_isLoggedAsOneEscapedLine_andNothingPropagates() {
        TestFactory factory = new TestFactory(new TestProducer(true));
        factory.onDestroy = new IllegalStateException("close failed\r\n2026-11-25 ERROR forged entry");
        KafkaAuditPublisher publisher = publisher(factory, FIVE_SECONDS);
        publisher.publish(updateEvent());

        Assertions.assertDoesNotThrow(publisher::destroy);

        List<ILoggingEvent> warnings = logs.list.stream().filter(e -> e.getLevel() == Level.WARN).toList();
        Assertions.assertEquals(1, warnings.size());
        String message = warnings.get(0).getFormattedMessage();
        Assertions.assertTrue(
                message.contains("IllegalStateException: close failed\\r\\n2026-11-25 ERROR forged entry"), message);
        Assertions.assertFalse(message.contains("\n") || message.contains("\r"), message);
        Assertions.assertNull(warnings.get(0).getThrowableProxy());
    }

    @Test
    void destroy_destroysTheProducerFactory() {
        TestFactory factory = new TestFactory(new TestProducer(true));
        KafkaAuditPublisher publisher = publisher(factory, FIVE_SECONDS);
        publisher.publish(updateEvent());

        publisher.destroy();

        Assertions.assertTrue(factory.destroyed);
    }

    @Test
    void destroy_beforeAnyPublish_createsNoFactory() {
        AtomicInteger calls = new AtomicInteger();
        KafkaAuditPublisher publisher = new KafkaAuditPublisher(() -> {
            calls.incrementAndGet();
            return new TestFactory(new TestProducer(true));
        }, FIVE_SECONDS, metrics, now::get);

        Assertions.assertDoesNotThrow(publisher::destroy);

        Assertions.assertEquals(0, calls.get());
        Assertions.assertFalse(publisher.producerFactoryCreated());
    }

    @Test
    void publishAfterDestroy_isCountedAndLogged_andCreatesNoProducer() {
        TestProducer producer = new TestProducer(true);
        TestFactory factory = new TestFactory(producer);
        AtomicInteger supplied = new AtomicInteger();
        KafkaAuditPublisher publisher = new KafkaAuditPublisher(() -> {
            supplied.incrementAndGet();
            return factory;
        }, FIVE_SECONDS, metrics, now::get);
        publisher.publish(updateEvent());
        int producersBefore = factory.created.get();
        publisher.destroy();
        AuditEventMessage late = createEvent();

        Assertions.assertDoesNotThrow(() -> publisher.publish(late));

        Assertions.assertEquals(1, supplied.get());
        Assertions.assertEquals(producersBefore, factory.created.get(), "no producer is created after destroy");
        Assertions.assertEquals(1, producer.history().size(), "nothing is sent after destroy");
        List<ILoggingEvent> errors = errors();
        Assertions.assertEquals(1, errors.size());
        String message = errors.get(0).getFormattedMessage();
        Assertions.assertTrue(message.contains("closed"), message);
        Assertions.assertTrue(message.contains(AuditJson.logLine(late)), message);
        Assertions.assertEquals(1L, failedCount());
        Assertions.assertEquals(List.of("MeasureDefinition"), counter.resources());
    }

    @Test
    void producerConfig_setsTheFourOverrides_andDropsInheritedSerializersRetriesAndInFlight() {
        Map<String, Object> service = new HashMap<>();
        service.put(ProducerConfig.BOOTSTRAP_SERVERS_CONFIG, "kafka:9092");
        service.put("security.protocol", "SASL_SSL");
        service.put(ProducerConfig.KEY_SERIALIZER_CLASS_CONFIG, StringSerializer.class);
        service.put(ProducerConfig.VALUE_SERIALIZER_CLASS_CONFIG, JsonSerializer.class);
        service.put(ProducerConfig.RETRIES_CONFIG, 0);
        service.put(ProducerConfig.MAX_IN_FLIGHT_REQUESTS_PER_CONNECTION, 10);
        service.put(ProducerConfig.MAX_BLOCK_MS_CONFIG, 60_000);
        service.put(ProducerConfig.ENABLE_IDEMPOTENCE_CONFIG, false);
        service.put(ProducerConfig.ACKS_CONFIG, "1");
        Map<String, Object> before = new HashMap<>(service);

        Map<String, Object> config = KafkaAuditPublisher.producerConfig(service, Duration.ofSeconds(3));

        Assertions.assertEquals(3_000L, config.get(ProducerConfig.MAX_BLOCK_MS_CONFIG));
        Assertions.assertEquals(true, config.get(ProducerConfig.ENABLE_IDEMPOTENCE_CONFIG));
        Assertions.assertEquals("all", config.get(ProducerConfig.ACKS_CONFIG));
        Assertions.assertEquals("audit", config.get(ProducerConfig.CLIENT_ID_CONFIG));
        Assertions.assertFalse(config.containsKey(ProducerConfig.KEY_SERIALIZER_CLASS_CONFIG));
        Assertions.assertFalse(config.containsKey(ProducerConfig.VALUE_SERIALIZER_CLASS_CONFIG));
        Assertions.assertFalse(config.containsKey(ProducerConfig.RETRIES_CONFIG));
        Assertions.assertFalse(config.containsKey(ProducerConfig.MAX_IN_FLIGHT_REQUESTS_PER_CONNECTION));
        Assertions.assertEquals("kafka:9092", config.get(ProducerConfig.BOOTSTRAP_SERVERS_CONFIG));
        Assertions.assertEquals("SASL_SSL", config.get("security.protocol"));
        Assertions.assertEquals(before, service, "the service's map is not changed");
    }

    /** A transactional factory refuses a send made outside a transaction, which every audit send is. */
    @Test
    void producerConfig_dropsAnInheritedTransactionalId_soTheFactoryIsNotTransactional() {
        Map<String, Object> service = new HashMap<>();
        service.put(ProducerConfig.BOOTSTRAP_SERVERS_CONFIG, "localhost:9092");
        service.put(ProducerConfig.TRANSACTIONAL_ID_CONFIG, "validation-tx");

        Map<String, Object> config = KafkaAuditPublisher.producerConfig(service, Duration.ofSeconds(3));

        Assertions.assertFalse(config.containsKey(ProducerConfig.TRANSACTIONAL_ID_CONFIG));
        Assertions.assertEquals("localhost:9092", config.get(ProducerConfig.BOOTSTRAP_SERVERS_CONFIG));
        Assertions.assertFalse(KafkaAuditPublisher.producerFactory(config).transactionCapable());
        Assertions.assertEquals("validation-tx", service.get(ProducerConfig.TRANSACTIONAL_ID_CONFIG),
                "the service's own configuration is not changed");
    }

    /** What a service sets for its own JSON serialiser must not reach the audit one, which is set up in code. */
    @Test
    void producerFactory_doesNotApplyInheritedJsonSerializerSettings() {
        Map<String, Object> service = new HashMap<>();
        service.put(ProducerConfig.BOOTSTRAP_SERVERS_CONFIG, "localhost:9092");
        service.put(JsonSerializer.ADD_TYPE_INFO_HEADERS, true);
        service.put(JsonSerializer.TYPE_MAPPINGS, "event:" + AuditEventMessage.class.getName());
        DefaultKafkaProducerFactory<String, AuditEventMessage> factory =
                (DefaultKafkaProducerFactory<String, AuditEventMessage>) KafkaAuditPublisher.producerFactory(
                        KafkaAuditPublisher.producerConfig(service, Duration.ofSeconds(3)));
        try {
            // Creating the producer is where the factory would configure the serialisers.
            Producer<String, AuditEventMessage> producer = Assertions.assertDoesNotThrow(() -> factory.createProducer());
            producer.close(Duration.ZERO);

            RecordHeaders headers = new RecordHeaders();
            factory.getValueSerializer().serialize(Topics.AUDITABLE_EVENT_OCCURRED, headers, updateEvent());
            Assertions.assertNull(headers.lastHeader("__TypeId__"));
        } finally {
            factory.destroy();
        }
    }

    @Test
    void producerConfig_clientId_appendsAudit() {
        Map<String, Object> config = KafkaAuditPublisher.producerConfig(
                Map.of(ProducerConfig.CLIENT_ID_CONFIG, "validation"), Duration.ofSeconds(3));

        Assertions.assertEquals("validation-audit", config.get(ProducerConfig.CLIENT_ID_CONFIG));
    }

    @Test
    void producerConfig_noClientId_isAudit() {
        Map<String, Object> config = KafkaAuditPublisher.producerConfig(Map.of(), Duration.ofSeconds(3));

        Assertions.assertEquals("audit", config.get(ProducerConfig.CLIENT_ID_CONFIG));
    }

    @Test
    void producerFactory_isADefaultKafkaProducerFactory_withTheAuditSerializers() throws IOException {
        ProducerFactory<String, AuditEventMessage> factory = KafkaAuditPublisher.producerFactory(
                KafkaAuditPublisher.producerConfig(
                        Map.of(ProducerConfig.BOOTSTRAP_SERVERS_CONFIG, "localhost:9092"), Duration.ofSeconds(3)));

        Assertions.assertInstanceOf(DefaultKafkaProducerFactory.class, factory);
        DefaultKafkaProducerFactory<String, AuditEventMessage> defaultFactory =
                (DefaultKafkaProducerFactory<String, AuditEventMessage>) factory;
        Assertions.assertInstanceOf(StringSerializer.class, defaultFactory.getKeySerializer());
        Assertions.assertInstanceOf(JsonSerializer.class, defaultFactory.getValueSerializer());
        RecordHeaders headers = new RecordHeaders();
        byte[] bytes = defaultFactory.getValueSerializer().serialize(Topics.AUDITABLE_EVENT_OCCURRED, headers, updateEvent());
        Assertions.assertNull(headers.lastHeader("__TypeId__"));
        ObjectMapper plain = new ObjectMapper();
        Assertions.assertEquals(readFixture(plain, "audit-event-update.json"), plain.readTree(bytes));
        Assertions.assertEquals(3_000L, defaultFactory.getConfigurationProperties().get(ProducerConfig.MAX_BLOCK_MS_CONFIG));
    }

    private KafkaAuditPublisher publisher(TestFactory factory, Duration timeout) {
        return new KafkaAuditPublisher(() -> factory, timeout, metrics, now::get);
    }

    private static AuditEventMessage updateEvent() {
        return new AuditEventMessage(
                "Hospital1", "Validation", null, EVENT_DATE, null, "nurse@hospital.example.org",
                AuditAction.UPDATE, "Category", "missing-required-element", "Patient/123",
                List.of(new PropertyChange("Title", "Old", "New"),
                        new PropertyChange("ApiKey", "[redacted]", "[redacted]")),
                null);
    }

    private static AuditEventMessage createEvent() {
        return new AuditEventMessage(
                null, "MeasureEval", null, EVENT_DATE, null, null,
                AuditAction.CREATE, "MeasureDefinition", "measure-1", null,
                null, "measure-1 created");
    }

    private JsonNode readFixture(ObjectMapper mapper, String name) throws IOException {
        try (InputStream stream = getClass().getResourceAsStream("/audit/" + name)) {
            Assertions.assertNotNull(stream, "Missing fixture " + name);
            return mapper.readTree(stream);
        }
    }

    private static long timeMillis(Runnable action) {
        long start = System.nanoTime();
        action.run();
        return Duration.ofNanos(System.nanoTime() - start).toMillis();
    }

    private List<ILoggingEvent> errors() {
        return logs.list.stream().filter(e -> e.getLevel() == Level.ERROR).toList();
    }

    private long failedCount() {
        return counter.total();
    }

    /**
     * A {@link MockProducer} that survives the close a {@code KafkaTemplate} makes after every send to a
     * producer not from {@code DefaultKafkaProducerFactory}, and can run a hook at the start of a send.
     */
    private static final class TestProducer extends MockProducer<String, AuditEventMessage> {
        private volatile Runnable onSend = () -> { };
        private final AtomicInteger failed = new AtomicInteger();

        TestProducer(boolean autoComplete) {
            super(autoComplete, new StringSerializer(), KafkaAuditPublisher.valueSerializer());
        }

        @Override
        public synchronized Future<RecordMetadata> send(ProducerRecord<String, AuditEventMessage> record,
                                                        Callback callback) {
            onSend.run();
            return super.send(record, callback);
        }

        @Override
        public void close() {
        }

        @Override
        public void close(Duration timeout) {
        }
    }

    private static final class TestFactory implements ProducerFactory<String, AuditEventMessage>, DisposableBean {
        private final Producer<String, AuditEventMessage> producer;
        private volatile boolean destroyed;
        private volatile RuntimeException onDestroy;
        private final AtomicInteger created = new AtomicInteger();

        TestFactory(Producer<String, AuditEventMessage> producer) {
            this.producer = producer;
        }

        @Override
        public Producer<String, AuditEventMessage> createProducer() {
            created.incrementAndGet();
            return producer;
        }

        @Override
        public Map<String, Object> getConfigurationProperties() {
            return Collections.emptyMap();
        }

        @Override
        public void destroy() {
            destroyed = true;
            if (onDestroy != null) {
                throw onDestroy;
            }
        }
    }
}
