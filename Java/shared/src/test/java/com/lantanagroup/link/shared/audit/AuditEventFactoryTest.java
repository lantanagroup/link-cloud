package com.lantanagroup.link.shared.audit;

import com.lantanagroup.link.shared.auth.PrincipalUser;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;
import org.springframework.security.authentication.TestingAuthenticationToken;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;

import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

public class AuditEventFactoryTest {
    private static final Instant NOW = Instant.parse("2026-11-23T15:04:05.123456Z");

    @SuppressWarnings("unused")
    static class Holder {
        @Auditable(action = AuditAction.CREATE, resource = "Category")
        void create() {
        }

        @Auditable(action = AuditAction.UPDATE, resource = "Category")
        void update() {
        }

        @Auditable(action = AuditAction.UPDATE, resource = "Category", propertyChanges = false)
        void updateNoChanges() {
        }

        @Auditable(action = AuditAction.UPDATE, resource = "Category", recordIdVariable = "categoryId")
        void customId() {
        }

        @Auditable(action = AuditAction.DELETE, resource = "Artifact",
                note = "Artifact (Type: {type}, Name: {name}) deleted")
        void noted() {
        }
    }

    private final AuditProperties properties = new AuditProperties();
    private final AuditEventFactory factory;

    public AuditEventFactoryTest() {
        properties.setServiceName("validation");
        factory = new AuditEventFactory(properties, Clock.fixed(NOW, ZoneOffset.UTC));
    }

    private static Auditable annotation(String method) {
        try {
            return Holder.class.getDeclaredMethod(method).getAnnotation(Auditable.class);
        } catch (NoSuchMethodException e) {
            throw new AssertionError(e);
        }
    }

    private AuditEventMessage create(String method, AuditState state, Map<String, String> vars,
                                     Authentication authentication) {
        return factory.create(annotation(method), state, vars, authentication);
    }

    private AuditEventMessage create(String method, AuditState state, Map<String, String> vars) {
        return create(method, state, vars, null);
    }

    private static Authentication principal(Object principal) {
        return new UsernamePasswordAuthenticationToken(principal, "", List.of());
    }

    @Test
    public void serviceNameAndEventDate() {
        AuditEventMessage message = create("create", new AuditState(), null);
        Assertions.assertEquals("validation", message.serviceName());
        Assertions.assertEquals(Instant.parse("2026-11-23T15:04:05.123Z"), message.eventDate());
        Assertions.assertEquals("Category", message.resource());
    }

    @Test
    public void action_overrideWinsOverAnnotation() {
        AuditState state = new AuditState();
        Assertions.assertEquals(AuditAction.CREATE, create("create", state, null).action());
        state.actionOverride(AuditAction.UPDATE);
        Assertions.assertEquals(AuditAction.UPDATE, create("create", state, null).action());
    }

    @Test
    public void resourceId_handlerThenPathVariableThenNull() {
        AuditState state = new AuditState();
        Assertions.assertNull(create("create", state, Map.of()).resourceId());
        Assertions.assertEquals("p1", create("create", state, Map.of("id", "p1")).resourceId());
        state.recordId("h1");
        Assertions.assertEquals("h1", create("create", state, Map.of("id", "p1")).resourceId());
    }

    @Test
    public void resourceId_neverFallsBackToFacility() {
        AuditState state = new AuditState();
        state.facilityId("f1");
        AuditEventMessage message = create("create", state, Map.of("facilityId", "f2"));
        Assertions.assertNull(message.resourceId());
        Assertions.assertEquals("f1", message.facilityId());
    }

    @Test
    public void resourceId_customVariableName() {
        AuditState state = new AuditState();
        Map<String, String> vars = Map.of("id", "wrong", "categoryId", "c9");
        Assertions.assertEquals("c9", create("customId", state, vars).resourceId());
    }

    @Test
    public void facilityId_handlerThenPathVariableThenNull() {
        AuditState state = new AuditState();
        Assertions.assertNull(create("create", state, null).facilityId());
        Assertions.assertEquals("f2", create("create", state, Map.of("facilityId", "f2")).facilityId());
        state.facilityId("f1");
        Assertions.assertEquals("f1", create("create", state, Map.of("facilityId", "f2")).facilityId());
    }

    @Test
    public void user_principalUserEmail() {
        Authentication auth = principal(new PrincipalUser("sub", "a@b.org", List.of()));
        Assertions.assertEquals("a@b.org", create("create", new AuditState(), null, auth).user());
    }

    @Test
    public void user_blankEmail_isNull() {
        Authentication auth = principal(new PrincipalUser("sub", " ", List.of()));
        Assertions.assertNull(create("create", new AuditState(), null, auth).user());
        Authentication none = principal(new PrincipalUser("sub", null, List.of()));
        Assertions.assertNull(create("create", new AuditState(), null, none).user());
    }

    @Test
    public void user_otherPrincipalType_isNull() {
        Authentication auth = new TestingAuthenticationToken("someone@b.org", "x");
        Assertions.assertNull(create("create", new AuditState(), null, auth).user());
    }

    @Test
    public void user_nullAuthentication_isNull() {
        Assertions.assertNull(create("create", new AuditState(), null, null).user());
    }

    @Test
    public void userIdAndCorrelationId_alwaysNull() {
        Authentication auth = principal(new PrincipalUser("sub", "a@b.org", List.of()));
        AuditEventMessage message = create("create", new AuditState(), null, auth);
        Assertions.assertNull(message.userId());
        Assertions.assertNull(message.correlationId());
    }

    @Test
    public void notes_emptyTemplate_isNull() {
        Assertions.assertNull(create("create", new AuditState(), null).notes());
    }

    @Test
    public void notes_suppressed_isNull() {
        AuditState state = new AuditState();
        state.suppressNote();
        Assertions.assertNull(create("noted", state, Map.of("type", "t", "name", "n")).notes());
    }

    @Test
    public void notes_filledFromHandlerValueIgnoringCase() {
        AuditState state = new AuditState();
        state.values().put("TYPE", "measure");
        AuditEventMessage message = create("noted", state, Map.of("name", "n1"));
        Assertions.assertEquals("Artifact (Type: measure, Name: n1) deleted", message.notes());
    }

    @Test
    public void notes_handlerValueBeatsPathVariable() {
        AuditState state = new AuditState();
        state.values().put("type", "fromHandler");
        AuditEventMessage message = create("noted", state, Map.of("type", "fromPath", "name", "n"));
        Assertions.assertEquals("Artifact (Type: fromHandler, Name: n) deleted", message.notes());
    }

    @Test
    public void notes_nullValue_fillsEmpty() {
        AuditState state = new AuditState();
        state.values().put("type", null);
        AuditEventMessage message = create("noted", state, Map.of("type", "fromPath", "name", "n"));
        Assertions.assertEquals("Artifact (Type: , Name: n) deleted", message.notes());
    }

    @Test
    public void notes_unknownPlaceholder_staysVisible() {
        AuditEventMessage message = create("noted", new AuditState(), Map.of("name", "n"));
        Assertions.assertEquals("Artifact (Type: {type}, Name: n) deleted", message.notes());
    }

    @Test
    public void notes_valueWithDollarBackslashAndBraces_isLiteral() {
        AuditState state = new AuditState();
        state.values().put("name", "a$1\\b{type}");
        Map<String, String> vars = new HashMap<>();
        vars.put("type", "t");
        AuditEventMessage message = create("noted", state, vars);
        Assertions.assertEquals("Artifact (Type: t, Name: a$1\\b{type}) deleted", message.notes());
    }

    @Test
    public void propertyChanges_onlyWhenFinalActionIsUpdate() {
        AuditState state = new AuditState();
        state.changes().add(new PropertyChange("Title", "a", "b"));
        Assertions.assertNull(create("create", state, null).propertyChanges());

        state.actionOverride(AuditAction.UPDATE);
        Assertions.assertEquals(state.changes(), create("create", state, null).propertyChanges());

        state.actionOverride(AuditAction.CREATE);
        Assertions.assertNull(create("update", state, null).propertyChanges());
        state.actionOverride(null);
        Assertions.assertEquals(1, create("update", state, null).propertyChanges().size());
    }

    @Test
    public void propertyChanges_annotationFalse_isNull() {
        AuditState state = new AuditState();
        state.changes().add(new PropertyChange("Title", "a", "b"));
        Assertions.assertNull(create("updateNoChanges", state, null).propertyChanges());
    }

    @Test
    public void propertyChanges_empty_isNull() {
        Assertions.assertNull(create("update", new AuditState(), null).propertyChanges());
    }
}
