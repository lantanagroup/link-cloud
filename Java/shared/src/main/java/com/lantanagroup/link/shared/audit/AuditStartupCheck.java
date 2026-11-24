package com.lantanagroup.link.shared.audit;

import org.springframework.beans.factory.ObjectProvider;
import org.springframework.beans.factory.SmartInitializingSingleton;
import org.springframework.web.method.HandlerMethod;
import org.springframework.web.servlet.mvc.method.annotation.RequestMappingHandlerMapping;

import java.util.List;

/**
 * Fails the application's start when a controller method is marked {@link Auditable} but
 * {@code link.audit.service-name} is blank, so a service cannot publish events that name no service.
 */
final class AuditStartupCheck implements SmartInitializingSingleton {
    private final ObjectProvider<RequestMappingHandlerMapping> mappings;
    private final AuditProperties properties;

    AuditStartupCheck(ObjectProvider<RequestMappingHandlerMapping> mappings, AuditProperties properties) {
        this.mappings = mappings;
        this.properties = properties;
    }

    @Override
    public void afterSingletonsInstantiated() {
        String serviceName = properties.getServiceName();
        if (serviceName != null && !serviceName.isBlank()) {
            return;
        }
        List<String> marked = mappings.stream()
                .flatMap(mapping -> mapping.getHandlerMethods().values().stream())
                .filter(method -> method.hasMethodAnnotation(Auditable.class))
                .map(AuditStartupCheck::describe)
                .distinct()
                .sorted()
                .toList();
        if (!marked.isEmpty()) {
            throw new IllegalStateException(String.format(
                    "link.audit.service-name must be set: %d controller method(s) are marked @Auditable, first %s",
                    marked.size(), marked.get(0)));
        }
    }

    private static String describe(HandlerMethod method) {
        return method.getBeanType().getName() + "#" + method.getMethod().getName();
    }
}
