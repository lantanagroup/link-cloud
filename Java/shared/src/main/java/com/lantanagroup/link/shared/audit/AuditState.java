package com.lantanagroup.link.shared.audit;

import jakarta.servlet.http.HttpServletRequest;
import org.springframework.util.LinkedCaseInsensitiveMap;

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
    private final Map<String, Object> values = new LinkedCaseInsensitiveMap<>();
    private final List<PropertyChange> changes = new ArrayList<>();

    /** The state stored on the request, or a new empty one that is not stored. */
    static AuditState from(HttpServletRequest request) {
        Object stored = request == null ? null : request.getAttribute(ATTRIBUTE);
        return stored instanceof AuditState state ? state : new AuditState();
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
}
