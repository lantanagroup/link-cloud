package com.lantanagroup.link.shared.audit;

/**
 * What a handler can say about the request it is serving, for the audit event the request produces. Nothing
 * here throws into the handler.
 */
public interface AuditContext {
    /** The row's own id, when the route does not carry it. */
    void setRecordId(String id);

    /** The facility the request concerns, when the route does not carry it. */
    void setFacilityId(String facilityId);

    /** The patient the request concerns. */
    void setPatientId(String patientId);

    /** For create-or-update endpoints: says which of the two happened. */
    void overrideAction(AuditAction action);

    /** Drops the note, for when the note only fits the other branch. */
    void suppressNote();

    /** A value for a {name} placeholder in the note. */
    void set(String name, Object value);

    /** Records what differs between the two objects, masked. Call it before the save. */
    void changes(Object before, Object after);

    /** Records one value by hand, masked by name. */
    void change(String name, Object before, Object after);
}
