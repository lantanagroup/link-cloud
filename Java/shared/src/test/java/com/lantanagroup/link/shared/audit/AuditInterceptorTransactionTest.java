package com.lantanagroup.link.shared.audit;

import io.opentelemetry.api.OpenTelemetry;
import jakarta.servlet.ServletException;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.test.context.junit.jupiter.web.SpringJUnitWebConfig;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.transaction.PlatformTransactionManager;
import org.springframework.transaction.TransactionDefinition;
import org.springframework.transaction.annotation.EnableTransactionManagement;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.transaction.support.AbstractPlatformTransactionManager;
import org.springframework.transaction.support.DefaultTransactionStatus;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.context.WebApplicationContext;
import org.springframework.web.servlet.config.annotation.EnableWebMvc;
import org.springframework.web.servlet.config.annotation.InterceptorRegistry;
import org.springframework.web.servlet.config.annotation.WebMvcConfigurer;

import java.time.Clock;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

/**
 * Proves, by running it, that {@code afterCompletion} follows the commit of a {@code @Transactional} handler's
 * transaction, which the design otherwise only reads from Spring's source.
 */
@SpringJUnitWebConfig(AuditInterceptorTransactionTest.Config.class)
public class AuditInterceptorTransactionTest {
    @Autowired
    private WebApplicationContext context;

    @Autowired
    private Steps steps;

    private MockMvc mockMvc;

    /** What happened, in order: "commit", "rollback" and "publish". */
    static class Steps {
        final List<String> list = Collections.synchronizedList(new ArrayList<>());
    }

    static class RecordingTransactionManager extends AbstractPlatformTransactionManager {
        private final Steps steps;

        RecordingTransactionManager(Steps steps) {
            this.steps = steps;
        }

        @Override
        protected Object doGetTransaction() {
            return new Object();
        }

        @Override
        protected void doBegin(Object transaction, TransactionDefinition definition) {
        }

        @Override
        protected void doCommit(DefaultTransactionStatus status) {
            steps.list.add("commit");
        }

        @Override
        protected void doRollback(DefaultTransactionStatus status) {
            steps.list.add("rollback");
        }
    }

    static class RecordingPublisher implements AuditPublisher {
        private final Steps steps;

        RecordingPublisher(Steps steps) {
            this.steps = steps;
        }

        @Override
        public void publish(AuditEventMessage event) {
            steps.list.add("publish");
        }
    }

    @RestController
    static class TransactionalController {
        @PutMapping("/things/{id}")
        @Transactional
        @Auditable(action = AuditAction.UPDATE, resource = "Thing")
        public String update(@PathVariable String id) {
            return "ok";
        }

        @PutMapping("/things/{id}/broken")
        @Transactional
        @Auditable(action = AuditAction.UPDATE, resource = "Thing")
        public String broken(@PathVariable String id) {
            throw new IllegalStateException("handler failed");
        }
    }

    @Configuration
    @EnableWebMvc
    @EnableTransactionManagement
    static class Config implements WebMvcConfigurer {
        @Bean
        Steps steps() {
            return new Steps();
        }

        @Bean
        PlatformTransactionManager transactionManager(Steps steps) {
            return new RecordingTransactionManager(steps);
        }

        @Bean
        AuditInterceptor auditInterceptor(Steps steps) {
            AuditProperties properties = new AuditProperties();
            properties.setServiceName("Test");
            return new AuditInterceptor(new AuditEventFactory(properties, Clock.systemUTC()),
                    new RecordingPublisher(steps), new AuditMetrics(OpenTelemetry.noop(), "Test"));
        }

        @Bean
        TransactionalController transactionalController() {
            return new TransactionalController();
        }

        @Override
        public void addInterceptors(InterceptorRegistry registry) {
            registry.addInterceptor(auditInterceptor(steps()));
        }
    }

    @BeforeEach
    public void setUp() {
        steps.list.clear();
        mockMvc = MockMvcBuilders.webAppContextSetup(context).build();
    }

    @Test
    public void transactionalHandler_commitsBeforeThePublisherIsCalled() throws Exception {
        mockMvc.perform(put("/things/t-1")).andExpect(status().isOk());

        Assertions.assertEquals(List.of("commit", "publish"), steps.list);
    }

    @Test
    public void transactionalHandlerThatThrows_rollsBackAndPublishesNothing() {
        ServletException thrown = Assertions.assertThrows(ServletException.class,
                () -> mockMvc.perform(put("/things/t-2/broken")));

        Assertions.assertInstanceOf(IllegalStateException.class, thrown.getCause());
        Assertions.assertEquals(List.of("rollback"), steps.list);
    }
}
