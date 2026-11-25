package com.lantanagroup.link.shared.audit;

import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.web.method.HandlerMethod;
import org.springframework.web.servlet.HandlerInterceptor;
import org.springframework.web.servlet.HandlerMapping;

import java.util.HashMap;
import java.util.Map;

/**
 * Records an audit event for each successful call of a controller method marked {@link Auditable}. It acts
 * in {@code afterCompletion}, which runs once the handler has returned (so after its transaction, if any, has
 * committed) and the final status is known. A call succeeded when it did not end in an exception and its
 * status is 200 to 299. An exception that an {@code @ExceptionHandler} resolved is not one the call ended in:
 * Spring reports none, so the status that handler chose decides, and a 2xx one is recorded. Nothing here throws into the request: a failure is counted and logged.
 * <p>
 * Before the handler runs it marks the request as one whose saves are worth watching, which is what lets a
 * persistence listener collect property changes for audited requests only.
 */
public final class AuditInterceptor implements HandlerInterceptor {
    private static final Logger logger = LoggerFactory.getLogger(AuditInterceptor.class);

    private final AuditEventFactory factory;
    private final AuditPublisher publisher;
    private final AuditMetrics metrics;

    public AuditInterceptor(AuditEventFactory factory, AuditPublisher publisher, AuditMetrics metrics) {
        this.factory = factory;
        this.publisher = publisher;
        this.metrics = metrics;
    }

    @Override
    public boolean preHandle(HttpServletRequest request, HttpServletResponse response, Object handler) {
        try {
            if (handler instanceof HandlerMethod handlerMethod) {
                Auditable auditable = handlerMethod.getMethodAnnotation(Auditable.class);
                // Not narrowed to the annotation's action: a handler may turn a create into an update.
                if (auditable != null && auditable.propertyChanges()) {
                    AuditState.stored(request).captureChanges();
                }
            }
        } catch (RuntimeException e) {
            logger.warn("Audit could not prepare the request, so its event will carry no captured property "
                    + "changes: {}", e.getClass().getName());
        }
        return true;
    }

    @Override
    public void afterCompletion(HttpServletRequest request, HttpServletResponse response, Object handler,
                                Exception ex) {
        Auditable auditable = null;
        AuditEventMessage event = null;
        try {
            if (!(handler instanceof HandlerMethod handlerMethod)) {
                return;
            }
            auditable = handlerMethod.getMethodAnnotation(Auditable.class);
            if (auditable == null || ex != null) {
                return;
            }
            int status = response.getStatus();
            if (status < 200 || status > 299) {
                return;
            }
            Authentication authentication = SecurityContextHolder.getContext().getAuthentication();
            event = factory.create(auditable, AuditState.from(request), pathVariables(request), authentication);
            publisher.publish(event);
        } catch (Exception e) {
            String resource = auditable == null ? null : auditable.resource();
            AuditAction action = auditable == null ? null : auditable.action();
            metrics.publishFailed(resource);
            // The exception is described, not handed to the logger: its message, or that of a cause, may carry
            // text from the request, which must not reach the log unescaped.
            if (event == null) {
                logger.error("Audit event for {} {} could not be recorded ({}, thrown at {})", action, resource,
                        AuditJson.describe(e), AuditJson.thrownAt(e));
            } else {
                logger.error("Audit event for {} {} could not be recorded ({}, thrown at {}): {}", action,
                        resource, AuditJson.describe(e), AuditJson.thrownAt(e), AuditJson.logLine(event));
            }
        }
    }

    private static Map<String, String> pathVariables(HttpServletRequest request) {
        Object attribute = request.getAttribute(HandlerMapping.URI_TEMPLATE_VARIABLES_ATTRIBUTE);
        Map<String, String> result = new HashMap<>();
        if (attribute instanceof Map<?, ?> map) {
            map.forEach((key, value) -> {
                if (key != null) {
                    result.put(key.toString(), value == null ? null : value.toString());
                }
            });
        }
        return result;
    }
}
