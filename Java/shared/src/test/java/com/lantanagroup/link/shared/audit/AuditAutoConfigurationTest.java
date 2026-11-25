package com.lantanagroup.link.shared.audit;

import com.fasterxml.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;
import org.springframework.boot.autoconfigure.AutoConfigurations;
import org.springframework.boot.autoconfigure.kafka.KafkaProperties;
import org.springframework.boot.autoconfigure.web.servlet.DispatcherServletAutoConfiguration;
import org.springframework.boot.autoconfigure.web.servlet.WebMvcAutoConfiguration;
import org.springframework.boot.test.context.runner.ApplicationContextRunner;
import org.springframework.boot.test.context.runner.WebApplicationContextRunner;
import org.springframework.core.NestedExceptionUtils;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.kafka.core.ProducerFactory;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RestController;

import java.time.Clock;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

public class AuditAutoConfigurationTest {
    private final WebApplicationContextRunner runner = new WebApplicationContextRunner()
            .withConfiguration(AutoConfigurations.of(
                    DispatcherServletAutoConfiguration.class,
                    WebMvcAutoConfiguration.class,
                    AuditAutoConfiguration.class));

    @RestController
    static class MarkedController {
        @PutMapping("/things/{id}")
        @Auditable(action = AuditAction.UPDATE, resource = "Thing")
        public String update(@PathVariable String id) {
            return "ok";
        }
    }

    @RestController
    static class PlainController {
        @PutMapping("/plain/{id}")
        public String update(@PathVariable String id) {
            return "ok";
        }
    }

    static class RecordingPublisher implements AuditPublisher {
        final List<AuditEventMessage> events = Collections.synchronizedList(new ArrayList<>());

        @Override
        public void publish(AuditEventMessage event) {
            events.add(event);
        }
    }

    @Test
    public void markedHandlerWithoutServiceName_failsStartup() {
        runner.withUserConfiguration(MarkedController.class)
                .withBean(AuditPublisher.class, RecordingPublisher::new)
                .run(context -> {
                    assertThat(context).hasFailed();
                    Throwable cause = NestedExceptionUtils.getMostSpecificCause(context.getStartupFailure());
                    assertThat(cause).isInstanceOf(IllegalStateException.class);
                    assertThat(cause.getMessage())
                            .contains("link.audit.service-name")
                            .contains("1 controller method")
                            .contains(MarkedController.class.getName() + "#update");
                });
    }

    @Test
    public void markedHandlerWithServiceName_starts() {
        runner.withUserConfiguration(MarkedController.class)
                .withBean(AuditPublisher.class, RecordingPublisher::new)
                .withPropertyValues("link.audit.service-name=Test")
                .run(context -> {
                    assertThat(context).hasNotFailed();
                    assertThat(context).hasSingleBean(AuditInterceptor.class);
                });
    }

    @Test
    public void noMarkedHandler_startsWithoutServiceName_andCreatesNoProducer() {
        runner.withUserConfiguration(PlainController.class)
                .withBean(KafkaProperties.class, KafkaProperties::new)
                .run(context -> {
                    assertThat(context).hasNotFailed();
                    assertThat(context).getBean(AuditPublisher.class).isInstanceOf(KafkaAuditPublisher.class);
                    KafkaAuditPublisher publisher = (KafkaAuditPublisher) context.getBean(AuditPublisher.class);

                    MockMvc mockMvc = MockMvcBuilders.webAppContextSetup(context).build();
                    mockMvc.perform(put("/plain/1")).andExpect(status().is2xxSuccessful());

                    Assertions.assertFalse(publisher.producerFactoryCreated());
                });
    }

    @Test
    public void noKafkaProperties_stillStarts() {
        runner.withUserConfiguration(PlainController.class)
                .run(context -> {
                    assertThat(context).hasNotFailed();
                    assertThat(context).doesNotHaveBean(KafkaProperties.class);
                    assertThat(context).getBean(AuditPublisher.class).isInstanceOf(KafkaAuditPublisher.class);
                });
    }

    @Test
    public void userAuditPublisherBean_replacesTheKafkaOne() {
        runner.withBean(AuditPublisher.class, RecordingPublisher::new)
                .run(context -> {
                    assertThat(context).hasSingleBean(AuditPublisher.class);
                    assertThat(context).getBean(AuditPublisher.class).isInstanceOf(RecordingPublisher.class);
                    assertThat(context).doesNotHaveBean(KafkaAuditPublisher.class);
                });
    }

    @Test
    public void customizerBean_isApplied() {
        runner.withBean(AuditMaskingCustomizer.class, () -> additions -> additions.addName("FacilityPin"))
                .run(context -> assertThat(context.getBean(AuditMasking.class).names()).contains("FacilityPin"));
    }

    @Test
    public void interceptorIsRegistered() {
        RecordingPublisher publisher = new RecordingPublisher();
        runner.withUserConfiguration(MarkedController.class)
                .withBean(AuditPublisher.class, () -> publisher)
                .withPropertyValues("link.audit.service-name=Test")
                .run(context -> {
                    MockMvc mockMvc = MockMvcBuilders.webAppContextSetup(context).build();
                    mockMvc.perform(put("/things/42")).andExpect(status().isOk());

                    Assertions.assertEquals(1, publisher.events.size());
                    AuditEventMessage event = publisher.events.get(0);
                    Assertions.assertEquals("Test", event.serviceName());
                    Assertions.assertEquals("Thing", event.resource());
                    Assertions.assertEquals("42", event.resourceId());
                });
    }

    @Test
    public void registersNoClockObjectMapperOrKafkaTemplateBean() {
        runner.withUserConfiguration(PlainController.class)
                .run(context -> {
                    assertThat(context).hasNotFailed();
                    assertThat(context).hasSingleBean(AuditPublisher.class);
                    assertThat(context).doesNotHaveBean(Clock.class);
                    assertThat(context).doesNotHaveBean(ObjectMapper.class);
                    assertThat(context).doesNotHaveBean(KafkaTemplate.class);
                    assertThat(context).doesNotHaveBean(ProducerFactory.class);
                });
    }

    @Test
    public void nonWebApplication_registersNothing() {
        new ApplicationContextRunner()
                .withConfiguration(AutoConfigurations.of(AuditAutoConfiguration.class))
                .run(context -> {
                    assertThat(context).hasNotFailed();
                    assertThat(context).doesNotHaveBean(AuditProperties.class);
                    assertThat(context).doesNotHaveBean(AuditContext.class);
                    assertThat(context).doesNotHaveBean(AuditPublisher.class);
                    assertThat(context).doesNotHaveBean(AuditInterceptor.class);
                    assertThat(context).doesNotHaveBean(AuditStartupCheck.class);
                });
    }
}
