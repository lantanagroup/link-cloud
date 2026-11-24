package com.lantanagroup.link.shared.audit;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;
import org.springframework.mock.web.MockHttpServletRequest;
import org.springframework.web.context.request.RequestContextHolder;
import org.springframework.web.context.request.ServletRequestAttributes;

import java.util.List;

public class RequestAuditContextTest {
    private final RequestAuditContext context = new RequestAuditContext(new AuditDiff(new AuditMasking(List.of())));

    public static class Item {
        private String title;

        public Item(String title) {
            this.title = title;
        }

        public String getTitle() {
            return title;
        }

        public void setTitle(String title) {
            this.title = title;
        }
    }

    @AfterEach
    public void reset() {
        RequestContextHolder.resetRequestAttributes();
    }

    private static MockHttpServletRequest bind() {
        MockHttpServletRequest request = new MockHttpServletRequest();
        RequestContextHolder.setRequestAttributes(new ServletRequestAttributes(request));
        return request;
    }

    @Test
    public void writes_areVisibleThroughAuditStateFrom() {
        MockHttpServletRequest request = bind();

        context.setRecordId("r1");
        context.setFacilityId("f1");
        context.setPatientId("p1");
        context.overrideAction(AuditAction.CREATE);
        context.suppressNote();
        context.set("count", 3);
        context.changes(new Item("a"), new Item("b"));
        context.change("status", "x", "y");

        AuditState state = AuditState.from(request);
        Assertions.assertEquals("r1", state.recordId());
        Assertions.assertEquals("f1", state.facilityId());
        Assertions.assertEquals("p1", state.patientId());
        Assertions.assertEquals(AuditAction.CREATE, state.actionOverride());
        Assertions.assertTrue(state.noteSuppressed());
        Assertions.assertEquals(3, state.values().get("count"));
        Assertions.assertEquals(List.of(new PropertyChange("Title", "a", "b"), new PropertyChange("status", "x", "y")),
                state.changes());
    }

    @Test
    public void changes_comparesAtCallTime() {
        MockHttpServletRequest request = bind();
        Item before = new Item("old");
        Item after = new Item("new");

        context.changes(before, after);
        before.setTitle("new");

        Assertions.assertEquals(List.of(new PropertyChange("Title", "old", "new")), AuditState.from(request).changes());
    }

    @Test
    public void set_nameLookupIgnoresCase() {
        MockHttpServletRequest request = bind();

        context.set("FacilityName", "Mercy");

        Assertions.assertEquals("Mercy", AuditState.from(request).values().get("facilityname"));
    }

    @Test
    public void set_nullValue_isKept() {
        MockHttpServletRequest request = bind();

        context.set("name", null);

        Assertions.assertTrue(AuditState.from(request).values().containsKey("name"));
        Assertions.assertNull(AuditState.from(request).values().get("name"));
    }

    @Test
    public void set_blankName_isIgnored() {
        MockHttpServletRequest request = bind();

        context.set(null, "v");
        context.set(" ", "v");

        Assertions.assertTrue(AuditState.from(request).values().isEmpty());
    }

    @Test
    public void change_blankOrNullName_recordsNothing() {
        MockHttpServletRequest request = bind();

        context.change(null, "a", "b");
        context.change("", "a", "b");
        context.change("  ", "a", "b");

        Assertions.assertTrue(AuditState.from(request).changes().isEmpty());
        Assertions.assertNull(request.getAttribute(AuditState.ATTRIBUTE));
    }

    @Test
    public void noRequestBound_everyMethodIsANoOp() {
        Assertions.assertNull(RequestContextHolder.getRequestAttributes());

        Assertions.assertDoesNotThrow(() -> {
            context.setRecordId("r");
            context.setFacilityId("f");
            context.setPatientId("p");
            context.overrideAction(AuditAction.UPDATE);
            context.suppressNote();
            context.set("n", "v");
            context.changes(new Item("a"), new Item("b"));
            context.change("n", "a", "b");
        });
    }

    @Test
    public void from_requestWithoutState_returnsEmptyAndDoesNotStore() {
        MockHttpServletRequest request = new MockHttpServletRequest();

        AuditState state = AuditState.from(request);

        Assertions.assertNull(state.recordId());
        Assertions.assertNull(state.actionOverride());
        Assertions.assertFalse(state.noteSuppressed());
        Assertions.assertTrue(state.values().isEmpty());
        Assertions.assertTrue(state.changes().isEmpty());
        Assertions.assertNull(request.getAttribute(AuditState.ATTRIBUTE));
    }

    @Test
    public void twoRequests_doNotShareState() {
        MockHttpServletRequest first = bind();
        context.setRecordId("one");
        MockHttpServletRequest second = bind();
        context.setRecordId("two");

        Assertions.assertEquals("one", AuditState.from(first).recordId());
        Assertions.assertEquals("two", AuditState.from(second).recordId());
    }
}
