package com.lantanagroup.link.shared.audit;

import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

/**
 * Marks a property whose old and new values must be recorded as {@code [redacted]} in an audit event, in
 * addition to the names and patterns every service shares.
 * <p>
 * On a field it masks the property of the same name, whatever the case. Where the getter has another name
 * (field {@code socialSecurityNumber}, getter {@code getSsn}) put it on the getter: a marked field that names no
 * property cannot be matched to its getter, so every property of the class that has no field of its own name
 * is masked until it is.
 */
@Target({ElementType.FIELD, ElementType.METHOD})
@Retention(RetentionPolicy.RUNTIME)
public @interface AuditMask {
}
