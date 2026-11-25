package com.lantanagroup.link.shared.audit;

import com.fasterxml.jackson.annotation.JsonValue;

/**
 * The action an audit event describes, as the .NET {@code AuditEventType} names it.
 * <p>
 * The wire carries the .NET member name ({@code "Action":"SoftDelete"}), so each constant states its
 * name explicitly rather than relying on Jackson's handling of the Java constant name.
 */
public enum AuditAction {
    CREATE("Create"),
    UPDATE("Update"),
    DELETE("Delete"),
    QUERY("Query"),
    SUBMIT("Submit"),
    RESTORE("Restore"),
    SOFT_DELETE("SoftDelete");

    private final String wireName;

    AuditAction(String wireName) {
        this.wireName = wireName;
    }

    /** The name this action carries on {@code AuditableEventOccurred}. */
    @JsonValue
    public String wireName() {
        return wireName;
    }
}
