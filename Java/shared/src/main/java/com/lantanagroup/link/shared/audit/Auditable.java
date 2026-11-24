package com.lantanagroup.link.shared.audit;

import java.lang.annotation.Documented;
import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

/**
 * Marks a controller method whose successful calls are recorded as an audit event.
 * <p>
 * It has an effect only on a Spring MVC handler method (a controller method with a request mapping); on any
 * other method it records nothing, and the startup check does not see it.
 */
@Target(ElementType.METHOD)
@Retention(RetentionPolicy.RUNTIME)
@Documented
public @interface Auditable {
    /** What the endpoint does, unless the handler overrides it. */
    AuditAction action();

    /** What the endpoint acts on, as the REST API auditing ADR names it. */
    String resource();

    /** Note template with {@code {name}} placeholders; empty for no note. */
    String note() default "";

    /** The path variable that alone holds the row's own id. */
    String recordIdVariable() default "id";

    /** The path variable that holds the facility. */
    String facilityIdVariable() default "facilityId";

    /** Set false on an update whose recorded changes would be useless or too large. */
    boolean propertyChanges() default true;
}
