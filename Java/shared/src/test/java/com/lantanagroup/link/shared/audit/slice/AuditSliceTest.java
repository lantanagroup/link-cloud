package com.lantanagroup.link.shared.audit.slice;

import ch.qos.logback.classic.Level;
import ch.qos.logback.classic.Logger;
import ch.qos.logback.classic.spi.ILoggingEvent;
import ch.qos.logback.core.read.ListAppender;
import com.lantanagroup.link.shared.audit.AuditContext;
import com.lantanagroup.link.shared.audit.AuditEventMessage;
import com.lantanagroup.link.shared.audit.AuditInterceptor;
import com.lantanagroup.link.shared.audit.AuditPublisher;
import com.lantanagroup.link.shared.audit.KafkaAuditPublisher;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.autoconfigure.kafka.KafkaProperties;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.ApplicationContext;
import org.springframework.context.annotation.Bean;
import org.springframework.test.web.servlet.MockMvc;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

/**
 * The mechanism in a {@code @WebMvcTest} slice, set up as the Validation controller tests are: the slice applies
 * the auto-configuration, starts with no Kafka configuration, and a marked request still succeeds.
 */
// Shared has no bootstrap.yml; App Configuration is switched off here as each service's bootstrap.yml does.
@WebMvcTest(controllers = SliceController.class, properties = {
        "link.audit.service-name=Test",
        "spring.cloud.azure.appconfiguration.enabled=false"})
@AutoConfigureMockMvc(addFilters = false)
class AuditSliceTest {
    @Autowired
    private MockMvc mockMvc;

    @Autowired
    private ApplicationContext context;

    @Test
    void sliceStartsWithoutKafkaConfiguration() {
        Assertions.assertEquals(0, context.getBeanNamesForType(KafkaProperties.class).length);
        Assertions.assertNotNull(context.getBean(AuditContext.class));
        Assertions.assertNotNull(context.getBean(AuditInterceptor.class));
    }

    @Test
    void markedRequest_returns2xx_withNoKafka() throws Exception {
        Assertions.assertInstanceOf(KafkaAuditPublisher.class, context.getBean(AuditPublisher.class));

        Logger publisherLogger = (Logger) LoggerFactory.getLogger(KafkaAuditPublisher.class);
        ListAppender<ILoggingEvent> logs = new ListAppender<>();
        logs.start();
        publisherLogger.addAppender(logs);
        publisherLogger.setAdditive(false);
        try {
            mockMvc.perform(put("/slice/7")).andExpect(status().isOk());
        } finally {
            publisherLogger.detachAppender(logs);
            publisherLogger.setAdditive(true);
        }

        Assertions.assertEquals(1, logs.list.stream()
                .filter(event -> event.getLevel() == Level.ERROR)
                .filter(event -> event.getFormattedMessage().contains("could not be published"))
                .count());
    }

    @Nested
    class WithTestPublisher {
        @TestConfiguration
        static class RecordingPublisherConfiguration {
            @Bean
            RecordingPublisher recordingPublisher() {
                return new RecordingPublisher();
            }
        }

        // Injected here, not read from the enclosing instance: this class runs in its own context.
        @Autowired
        private MockMvc nestedMockMvc;

        @Autowired
        private ApplicationContext nestedContext;

        @Autowired
        private RecordingPublisher publisher;

        @Test
        void sliceWithTestPublisher_seesTheEvent() throws Exception {
            Assertions.assertSame(publisher, nestedContext.getBean(AuditPublisher.class));

            nestedMockMvc.perform(put("/slice/7")).andExpect(status().isOk());

            Assertions.assertEquals(1, publisher.events.size());
            AuditEventMessage event = publisher.events.get(0);
            Assertions.assertEquals("Test", event.serviceName());
            Assertions.assertEquals("Slice", event.resource());
            Assertions.assertEquals("7", event.resourceId());
            Assertions.assertEquals("patient-1", event.patientId());
        }
    }

    static class RecordingPublisher implements AuditPublisher {
        final List<AuditEventMessage> events = Collections.synchronizedList(new ArrayList<>());

        @Override
        public void publish(AuditEventMessage event) {
            events.add(event);
        }
    }
}
