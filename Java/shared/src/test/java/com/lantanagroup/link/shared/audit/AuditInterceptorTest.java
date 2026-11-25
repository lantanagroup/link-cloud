package com.lantanagroup.link.shared.audit;

import ch.qos.logback.classic.Level;
import ch.qos.logback.classic.Logger;
import ch.qos.logback.classic.spi.ILoggingEvent;
import ch.qos.logback.core.read.ListAppender;
import com.lantanagroup.link.shared.auth.PrincipalUser;
import io.opentelemetry.api.common.AttributeKey;
import io.opentelemetry.sdk.OpenTelemetrySdk;
import io.opentelemetry.sdk.metrics.SdkMeterProvider;
import io.opentelemetry.sdk.metrics.data.LongPointData;
import io.opentelemetry.sdk.metrics.data.MetricData;
import io.opentelemetry.sdk.testing.exporter.InMemoryMetricReader;
import jakarta.servlet.ServletException;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.slf4j.LoggerFactory;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.mock.web.MockHttpServletRequest;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.MvcResult;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.server.ResponseStatusException;

import java.time.Clock;
import java.time.Instant;
import java.time.ZoneId;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

public class AuditInterceptorTest {
    private static final AttributeKey<String> RESOURCE = AttributeKey.stringKey("resource");
    private static final Clock CLOCK = Clock.fixed(Instant.parse("2026-11-23T15:04:05Z"), ZoneOffset.UTC);

    private final InMemoryMetricReader reader = InMemoryMetricReader.create();
    private final AuditMetrics metrics = new AuditMetrics(OpenTelemetrySdk.builder()
            .setMeterProvider(SdkMeterProvider.builder().registerMetricReader(reader).build())
            .build(), "Test");
    private final RecordingPublisher publisher = new RecordingPublisher();
    private final ThingController controller =
            new ThingController(new RequestAuditContext(new AuditDiff(new AuditMasking(List.of()))));

    private final Logger interceptorLogger = (Logger) LoggerFactory.getLogger(AuditInterceptor.class);
    private final ListAppender<ILoggingEvent> logs = new ListAppender<>();

    public static class Item {
        private String title;

        public Item(String title) {
            this.title = title;
        }

        public String getTitle() {
            return title;
        }
    }

    static class ConflictException extends RuntimeException {
    }

    @RestController
    static class ThingController {
        private final RequestAuditContext audit;

        ThingController(RequestAuditContext audit) {
            this.audit = audit;
        }

        @PutMapping("/things/{id}")
        @Auditable(action = AuditAction.UPDATE, resource = "Thing")
        public String update(@PathVariable String id) {
            return "ok";
        }

        @DeleteMapping("/things/{id}")
        @ResponseStatus(HttpStatus.NO_CONTENT)
        @Auditable(action = AuditAction.DELETE, resource = "Thing")
        public void remove(@PathVariable String id) {
        }

        @PostMapping("/things/{id}/save")
        @Auditable(action = AuditAction.CREATE, resource = "Thing", note = "Thing {name} saved")
        public String save(@PathVariable String id) {
            audit.overrideAction(AuditAction.UPDATE);
            audit.set("name", "Widget");
            audit.changes(new Item("old"), new Item("new"));
            return "ok";
        }

        @GetMapping("/things/{id}/missing")
        @Auditable(action = AuditAction.QUERY, resource = "Thing")
        public String missing(@PathVariable String id) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND, "no such thing");
        }

        @PutMapping("/things/{id}/conflict")
        @Auditable(action = AuditAction.UPDATE, resource = "Thing")
        public String conflict(@PathVariable String id) {
            throw new ConflictException();
        }

        @PutMapping("/things/{id}/invalid")
        @Auditable(action = AuditAction.UPDATE, resource = "Thing")
        public ResponseEntity<String> invalid(@PathVariable String id) {
            return ResponseEntity.badRequest().body("invalid");
        }

        @PutMapping("/things/{id}/broken")
        @Auditable(action = AuditAction.UPDATE, resource = "Thing")
        public String broken(@PathVariable String id) {
            throw new IllegalStateException("unresolved");
        }

        @PutMapping("/things/{id}/plain")
        public String plain(@PathVariable String id) {
            return "ok";
        }

        @PutMapping("/things/{id}/replaced")
        @Auditable(action = AuditAction.UPDATE, resource = "Thing", propertyChanges = false)
        public String replaced(@PathVariable String id) {
            return "ok";
        }
    }

    @RestControllerAdvice
    static class ThingAdvice {
        @ExceptionHandler(ConflictException.class)
        public ResponseEntity<String> conflict(ConflictException e) {
            return ResponseEntity.status(HttpStatus.CONFLICT).body("conflict");
        }
    }

    static class RecordingPublisher implements AuditPublisher {
        final List<AuditEventMessage> events = Collections.synchronizedList(new ArrayList<>());

        @Override
        public void publish(AuditEventMessage event) {
            events.add(event);
        }
    }

    @BeforeEach
    public void captureLogs() {
        logs.start();
        interceptorLogger.addAppender(logs);
        interceptorLogger.setAdditive(false);
    }

    @AfterEach
    public void reset() {
        SecurityContextHolder.clearContext();
        interceptorLogger.detachAppender(logs);
        interceptorLogger.setAdditive(true);
    }

    private MockMvc mockMvc(AuditInterceptor interceptor) {
        return MockMvcBuilders.standaloneSetup(controller)
                .setControllerAdvice(new ThingAdvice())
                .addInterceptors(interceptor)
                .build();
    }

    private MockMvc mockMvc() {
        return mockMvc(interceptor(new AuditEventFactory(properties(), CLOCK), publisher));
    }

    private AuditInterceptor interceptor(AuditEventFactory factory, AuditPublisher auditPublisher) {
        return new AuditInterceptor(factory, auditPublisher, metrics);
    }

    private static AuditProperties properties() {
        AuditProperties properties = new AuditProperties();
        properties.setServiceName("Test");
        return properties;
    }

    private AuditEventMessage single() {
        Assertions.assertEquals(1, publisher.events.size());
        return publisher.events.get(0);
    }

    private long failures(String resource) {
        List<MetricData> collected = List.copyOf(reader.collectAllMetrics());
        long total = 0;
        for (MetricData metric : collected) {
            if (!AuditMetrics.PUBLISH_FAILED.equals(metric.getName())) {
                continue;
            }
            for (LongPointData point : metric.getLongSumData().getPoints()) {
                if (resource.equals(point.getAttributes().get(RESOURCE))) {
                    total += point.getValue();
                }
            }
        }
        return total;
    }

    private void assertOneErrorLogged() {
        Assertions.assertEquals(1, logs.list.size());
        ILoggingEvent logged = logs.list.get(0);
        Assertions.assertEquals(Level.ERROR, logged.getLevel());
        Assertions.assertTrue(logged.getFormattedMessage().contains("Thing"));
        Assertions.assertTrue(logged.getFormattedMessage().contains("UPDATE"));
        Assertions.assertNotNull(logged.getThrowableProxy());
    }

    @Test
    public void marked2xx_recordsOneEvent() throws Exception {
        mockMvc().perform(put("/things/t-1")).andExpect(status().isOk());

        AuditEventMessage event = single();
        Assertions.assertEquals(AuditAction.UPDATE, event.action());
        Assertions.assertEquals("Thing", event.resource());
        Assertions.assertEquals("t-1", event.resourceId());
        Assertions.assertEquals("Test", event.serviceName());
        Assertions.assertEquals(CLOCK.instant(), event.eventDate());
        Assertions.assertNull(event.user());
    }

    @Test
    public void marked204_recordsOneEvent() throws Exception {
        mockMvc().perform(delete("/things/t-2")).andExpect(status().isNoContent());

        AuditEventMessage event = single();
        Assertions.assertEquals(AuditAction.DELETE, event.action());
        Assertions.assertEquals("t-2", event.resourceId());
    }

    @Test
    public void handlerWritesToContext_areInTheEvent() throws Exception {
        mockMvc().perform(post("/things/t-3/save")).andExpect(status().isOk());

        AuditEventMessage event = single();
        Assertions.assertEquals(AuditAction.UPDATE, event.action());
        Assertions.assertEquals("Thing Widget saved", event.notes());
        Assertions.assertEquals(List.of(new PropertyChange("Title", "old", "new")), event.propertyChanges());
        Assertions.assertEquals("t-3", event.resourceId());
    }

    @Test
    public void markedResponseStatusException404_recordsNone() throws Exception {
        mockMvc().perform(get("/things/t-4/missing")).andExpect(status().isNotFound());

        Assertions.assertTrue(publisher.events.isEmpty());
    }

    @Test
    public void exceptionResolvedByExceptionHandlerTo409_recordsNone() throws Exception {
        mockMvc().perform(put("/things/t-5/conflict")).andExpect(status().isConflict());

        Assertions.assertTrue(publisher.events.isEmpty());
    }

    @Test
    public void markedReturningResponseEntity400_recordsNone() throws Exception {
        mockMvc().perform(put("/things/t-6/invalid")).andExpect(status().isBadRequest());

        Assertions.assertTrue(publisher.events.isEmpty());
    }

    @Test
    public void unresolvedException_recordsNone() {
        MockMvc mockMvc = mockMvc();

        ServletException thrown = Assertions.assertThrows(ServletException.class,
                () -> mockMvc.perform(put("/things/t-7/broken")));

        Assertions.assertInstanceOf(IllegalStateException.class, thrown.getCause());
        Assertions.assertTrue(publisher.events.isEmpty());
        Assertions.assertTrue(logs.list.isEmpty());
    }

    @Test
    public void unmarked2xx_recordsNoneAndCreatesNoState() throws Exception {
        MvcResult result = mockMvc().perform(put("/things/t-8/plain")).andExpect(status().isOk()).andReturn();

        Assertions.assertTrue(publisher.events.isEmpty());
        Assertions.assertNull(result.getRequest().getAttribute(AuditState.ATTRIBUTE));
    }

    @Test
    public void nonHandlerMethodHandler_recordsNone() {
        AuditInterceptor interceptor = interceptor(new AuditEventFactory(properties(), CLOCK), publisher);
        MockHttpServletResponse response = new MockHttpServletResponse();
        response.setStatus(200);

        Assertions.assertDoesNotThrow(() ->
                interceptor.afterCompletion(new MockHttpServletRequest(), response, new Object(), null));

        Assertions.assertTrue(publisher.events.isEmpty());
        Assertions.assertTrue(logs.list.isEmpty());
    }

    @Test
    public void factoryThrows_responseStill2xx_andFailureCounted() throws Exception {
        Clock broken = new Clock() {
            @Override
            public ZoneId getZone() {
                return ZoneOffset.UTC;
            }

            @Override
            public Clock withZone(ZoneId zone) {
                return this;
            }

            @Override
            public Instant instant() {
                throw new IllegalStateException("clock failed");
            }
        };

        mockMvc(interceptor(new AuditEventFactory(properties(), broken), publisher))
                .perform(put("/things/t-9")).andExpect(status().isOk());

        Assertions.assertTrue(publisher.events.isEmpty());
        Assertions.assertEquals(1L, failures("Thing"));
        assertOneErrorLogged();
        // No event was built, so the line names the action and resource only.
        Assertions.assertFalse(logs.list.get(0).getFormattedMessage().contains("\"Resource\""));
    }

    @Test
    public void publisherThrows_logsTheEvent() throws Exception {
        List<AuditEventMessage> attempted = new ArrayList<>();
        AuditPublisher throwing = event -> {
            attempted.add(event);
            throw new IllegalStateException("broker down");
        };

        mockMvc(interceptor(new AuditEventFactory(properties(), CLOCK), throwing))
                .perform(put("/things/t-12")).andExpect(status().isOk());

        Assertions.assertEquals(1, attempted.size());
        assertOneErrorLogged();
        String message = logs.list.get(0).getFormattedMessage();
        Assertions.assertTrue(message.contains(AuditJson.logLine(attempted.get(0))), message);
        Assertions.assertTrue(message.contains("t-12"), message);
    }

    @Test
    public void publisherThrows_responseStill2xx_andFailureCounted() throws Exception {
        AuditPublisher throwing = event -> {
            throw new IllegalStateException("broker down");
        };

        mockMvc(interceptor(new AuditEventFactory(properties(), CLOCK), throwing))
                .perform(put("/things/t-10")).andExpect(status().isOk());

        Assertions.assertEquals(1L, failures("Thing"));
        assertOneErrorLogged();
    }

    @Test
    public void authenticatedPrincipalUser_isTheUser() throws Exception {
        PrincipalUser user = new PrincipalUser("subject-1", "ada@example.com", List.of());
        SecurityContextHolder.getContext()
                .setAuthentication(new UsernamePasswordAuthenticationToken(user, null, List.of()));

        mockMvc().perform(put("/things/t-11")).andExpect(status().isOk());

        Assertions.assertEquals("ada@example.com", single().user());
    }

    @Test
    public void markedHandler_marksTheRequestForPropertyCapture() throws Exception {
        MvcResult result = mockMvc().perform(put("/things/t-12")).andExpect(status().isOk()).andReturn();

        Assertions.assertTrue(AuditState.from(result.getRequest()).capturingChanges());
    }

    @Test
    public void markedHandlerWithPropertyChangesOff_doesNotMarkTheRequest() throws Exception {
        MvcResult result = mockMvc().perform(put("/things/t-13/replaced")).andExpect(status().isOk()).andReturn();

        Assertions.assertFalse(AuditState.from(result.getRequest()).capturingChanges());
        Assertions.assertEquals(1, publisher.events.size());
    }

    @Test
    public void unmarkedHandler_leavesNoStateOnTheRequest() throws Exception {
        MvcResult result = mockMvc().perform(put("/things/t-14/plain")).andExpect(status().isOk()).andReturn();

        Assertions.assertNull(result.getRequest().getAttribute(AuditState.ATTRIBUTE));
    }
}
