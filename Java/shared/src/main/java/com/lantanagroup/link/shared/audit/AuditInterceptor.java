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
 * status is 200 to 299. Nothing here throws into the request: a failure is counted and logged.
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
            if (event == null) {
                logger.error("Audit event for {} {} could not be recorded", action, resource, e);
            } else {
                logger.error("Audit event for {} {} could not be recorded: {}", action, resource,
                        AuditJson.logLine(event), e);
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
