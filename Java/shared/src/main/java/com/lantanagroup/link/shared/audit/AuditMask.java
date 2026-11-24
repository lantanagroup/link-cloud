package com.lantanagroup.link.shared.audit;

import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

/**
 * Marks a property whose old and new values must be recorded as {@code [redacted]} in an audit event, in
 * addition to the names and patterns every service shares.
 */
@Target({ElementType.FIELD, ElementType.METHOD})
@Retention(RetentionPolicy.RUNTIME)
public @interface AuditMask {
}
