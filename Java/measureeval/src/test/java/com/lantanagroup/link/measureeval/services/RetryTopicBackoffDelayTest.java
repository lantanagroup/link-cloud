package com.lantanagroup.link.measureeval.services;

import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.autoconfigure.ImportAutoConfiguration;
import org.springframework.boot.autoconfigure.kafka.KafkaAutoConfiguration;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.kafka.annotation.EnableKafka;
import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.retrytopic.RetryTopicConfiguration;
import org.springframework.kafka.retrytopic.RetryTopicConfigurationBuilder;
import org.springframework.kafka.test.context.EmbeddedKafka;
import org.springframework.test.context.TestPropertySource;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * Behavior-side guard (async-retry-design.md §6, §13.6): asserts that the spring-kafka
 * backoff-aware {@code -Retry} listener actually <b>delays</b> redelivery by the configured backoff
 * on the pinned spring-kafka version. Paired with {@link RetryTopicRecovererTest} (which proves our
 * header bytes are read identically to the framework's), this catches the silent break where a
 * spring-kafka upgrade changes retry-topic backoff semantics.
 *
 * <p><b>NOTE:</b> this is an embedded-broker timing test ({@code @EmbeddedKafka} — it starts its own
 * in-process broker) — heavier and inherently timing-sensitive. It runs as part of the normal test
 * suite in CI. If it proves flaky on slower CI agents, widen the tolerance around
 * {@link #BACKOFF_MS} rather than disabling it. It validates the backoff MECHANISM the recoverer
 * depends on; it intentionally lets the framework drive the retry so it does not require the full
 * async-consumer wiring.</p>
 */
@SpringBootTest(classes = RetryTopicBackoffDelayTest.TestConfig.class)
@EmbeddedKafka(partitions = 1, topics = {RetryTopicBackoffDelayTest.TOPIC})
@TestPropertySource(properties = {
        "spring.kafka.bootstrap-servers=${spring.embedded.kafka.brokers}",
        "spring.kafka.consumer.group-id=retry-delay-test",
        "spring.kafka.consumer.auto-offset-reset=earliest",
        "spring.kafka.producer.key-serializer=org.apache.kafka.common.serialization.StringSerializer",
        "spring.kafka.producer.value-serializer=org.apache.kafka.common.serialization.StringSerializer",
        "spring.kafka.consumer.key-deserializer=org.apache.kafka.common.serialization.StringDeserializer",
        "spring.kafka.consumer.value-deserializer=org.apache.kafka.common.serialization.StringDeserializer"
})
class RetryTopicBackoffDelayTest {

    static final String TOPIC = "DelayTest";
    static final long BACKOFF_MS = 3_000L;

    static final List<Long> invocationTimes = Collections.synchronizedList(new ArrayList<>());
    static CountDownLatch latch;

    @EnableKafka
    @Configuration
    @ImportAutoConfiguration(KafkaAutoConfiguration.class)
    static class TestConfig {

        /** Mirrors production: a backoff-aware {@code -Retry} listener with a fixed backoff. */
        @Bean
        RetryTopicConfiguration retryTopicConfiguration(KafkaTemplate<Object, Object> template) {
            return RetryTopicConfigurationBuilder.newInstance()
                    .includeTopic(TOPIC)
                    .fixedBackOff(BACKOFF_MS)
                    .maxAttempts(2)                 // original attempt + 1 retry
                    .retryTopicSuffix("-Retry")
                    .dltSuffix("-Error")
                    .create(template);
        }

        @Bean
        Listener listener() {
            return new Listener();
        }
    }

    static class Listener {
        @KafkaListener(topics = TOPIC, groupId = "retry-delay-test")
        void consume(ConsumerRecord<String, String> record) {
            invocationTimes.add(System.currentTimeMillis());
            latch.countDown();
            // Always fail so the framework routes to -Retry and the backoff-aware listener redelivers.
            throw new RuntimeException("force retry");
        }
    }

    @Test
    void retryDelivery_isDelayedByBackoff(@Autowired KafkaTemplate<Object, Object> template) throws Exception {
        latch = new CountDownLatch(2);   // original + one retry
        invocationTimes.clear();

        template.send(TOPIC, "key", "payload");

        assertTrue(latch.await(30, TimeUnit.SECONDS),
                "expected the original delivery and one retry within 30s");

        long gap = invocationTimes.get(1) - invocationTimes.get(0);
        // Allow generous slack for scheduler/broker latency; the point is the retry is NOT immediate.
        assertTrue(gap >= (long) (BACKOFF_MS * 0.8),
                "retry should be delayed ~" + BACKOFF_MS + "ms by the backoff, but gap was " + gap + "ms");
    }
}
