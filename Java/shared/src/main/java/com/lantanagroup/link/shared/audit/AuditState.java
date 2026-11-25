package com.lantanagroup.link.shared.audit;

import jakarta.servlet.http.HttpServletRequest;
import org.springframework.util.LinkedCaseInsensitiveMap;
import org.springframework.web.context.request.RequestAttributes;
import org.springframework.web.context.request.RequestContextHolder;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;

/**
 * What a handler said about one request through {@link AuditContext}. A plain mutable holder, one per request,
 * kept as a request attribute.
 */
final class AuditState {
    static final String ATTRIBUTE = AuditState.class.getName();

    private String recordId;
    private String facilityId;
    private String patientId;
    private AuditAction actionOverride;
    private boolean noteSuppressed;
    private boolean capturingChanges;
    private final Map<String, Object> values = new LinkedCaseInsensitiveMap<>();
    private final List<PropertyChange> changes = new ArrayList<>();

    /** The state stored on the request, or a new empty one that is not stored. */
    static AuditState from(HttpServletRequest request) {
        Object stored = request == null ? null : request.getAttribute(ATTRIBUTE);
        return stored instanceof AuditState state ? state : new AuditState();
    }

    /** The state stored on the request, created and stored first if the request has none. */
    static AuditState stored(HttpServletRequest request) {
        if (request.getAttribute(ATTRIBUTE) instanceof AuditState state) {
            return state;
        }
        AuditState state = new AuditState();
        request.setAttribute(ATTRIBUTE, state);
        return state;
    }

    /**
     * The state of the request this thread is serving, or null when the thread serves none or the request
     * has none. It throws if the request has already completed.
     */
    static AuditState current() {
        RequestAttributes attributes = RequestContextHolder.getRequestAttributes();
        if (attributes == null) {
            return null;
        }
        return attributes.getAttribute(ATTRIBUTE, RequestAttributes.SCOPE_REQUEST) instanceof AuditState state
                ? state : null;
    }

    String recordId() {
        return recordId;
    }

    String facilityId() {
        return facilityId;
    }

    String patientId() {
        return patientId;
    }

    /** Null when the handler did not override the action. */
    AuditAction actionOverride() {
        return actionOverride;
    }

    boolean noteSuppressed() {
        return noteSuppressed;
    }

    /** Placeholder values; keys ignore case and values may be null. */
    Map<String, Object> values() {
        return values;
    }

    List<PropertyChange> changes() {
        return changes;
    }

    void recordId(String recordId) {
        this.recordId = recordId;
    }

    void facilityId(String facilityId) {
        this.facilityId = facilityId;
    }

    void patientId(String patientId) {
        this.patientId = patientId;
    }

    void actionOverride(AuditAction actionOverride) {
        this.actionOverride = actionOverride;
    }

    void suppressNote() {
        this.noteSuppressed = true;
    }

    /**
     * True while the request is served by a handler whose event may carry property changes. Only then are the
     * changes a save makes collected: a save no audited request wraps would be collected for nobody.
     */
    boolean capturingChanges() {
        return capturingChanges;
    }

    void captureChanges() {
        this.capturingChanges = true;
    }
}
