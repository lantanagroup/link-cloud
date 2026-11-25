package com.lantanagroup.link.shared.audit;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.web.context.request.RequestAttributes;
import org.springframework.web.context.request.RequestContextHolder;

import java.util.function.Consumer;

/**
 * The {@link AuditContext} of the request being served. Finds the request's {@link AuditState} through the
 * request attributes, creating it on first write; with no request bound it does nothing.
 */
public final class RequestAuditContext implements AuditContext {
    private static final Logger logger = LoggerFactory.getLogger(RequestAuditContext.class);

    private final AuditDiff diff;

    RequestAuditContext(AuditDiff diff) {
        this.diff = diff;
    }

    @Override
    public void setRecordId(String id) {
        write("setRecordId", state -> state.recordId(id));
    }

    @Override
    public void setFacilityId(String facilityId) {
        write("setFacilityId", state -> state.facilityId(facilityId));
    }

    @Override
    public void setPatientId(String patientId) {
        write("setPatientId", state -> state.patientId(patientId));
    }

    @Override
    public void overrideAction(AuditAction action) {
        write("overrideAction", state -> state.actionOverride(action));
    }

    @Override
    public void suppressNote() {
        write("suppressNote", AuditState::suppressNote);
    }

    @Override
    public void set(String name, Object value) {
        if (name == null || name.isBlank()) {
            return;
        }
        write("set", state -> state.values().put(name, value));
    }

    @Override
    public void changes(Object before, Object after) {
        write("changes", state -> state.changes().addAll(diff.diff(before, after)));
    }

    @Override
    public void change(String name, Object before, Object after) {
        if (name == null || name.isBlank()) {
            logger.debug("AuditContext.change ignored: the property name is blank");
            return;
        }
        write("change", state -> state.changes().add(diff.single(name, before, after)));
    }

    private void write(String method, Consumer<AuditState> action) {
        try {
            RequestAttributes attributes = RequestContextHolder.getRequestAttributes();
            if (attributes == null) {
                logger.debug("AuditContext.{} ignored: no request is bound to this thread", method);
                return;
            }
            Object stored = attributes.getAttribute(AuditState.ATTRIBUTE, RequestAttributes.SCOPE_REQUEST);
            AuditState state;
            if (stored instanceof AuditState existing) {
                state = existing;
            } else {
                state = new AuditState();
                attributes.setAttribute(AuditState.ATTRIBUTE, state, RequestAttributes.SCOPE_REQUEST);
            }
            action.accept(state);
        } catch (RuntimeException e) {
            logger.warn("AuditContext.{} failed and was ignored: {}", method, e.getClass().getName());
        }
    }
}
