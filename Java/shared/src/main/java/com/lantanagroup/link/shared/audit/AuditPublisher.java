package com.lantanagroup.link.shared.audit;

/**
 * The one way an audit event leaves a service. Implementations never throw: a failure to publish is
 * counted and logged, never raised into the request that produced the event.
 */
public interface AuditPublisher {
    void publish(AuditEventMessage event);
}
