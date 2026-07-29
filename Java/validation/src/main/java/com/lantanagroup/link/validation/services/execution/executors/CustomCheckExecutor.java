package com.lantanagroup.link.validation.services.execution.executors;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.services.execution.CheckExecutor;
import com.lantanagroup.link.validation.services.execution.spi.CustomCheck;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Component;

import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

@Component
@Slf4j
public class CustomCheckExecutor implements CheckExecutor {

    private final ObjectMapper objectMapper;
    private final Map<String, CustomCheck> byId = new ConcurrentHashMap<>();

    public CustomCheckExecutor(List<CustomCheck> customChecks, ObjectMapper objectMapper) {
        this.objectMapper = objectMapper;
        for (CustomCheck cc : customChecks) {
            CustomCheck previous = byId.put(cc.id(), cc);
            if (previous != null) {
                log.warn("Duplicate CustomCheck id '{}' — {} overrides {}",
                        cc.id(), cc.getClass().getName(), previous.getClass().getName());
            }
        }
        log.info("Registered {} CustomCheck plug-in(s): {}", byId.size(), byId.keySet());
    }

    @Override
    public CheckType supports() {
        return CheckType.CUSTOM;
    }

    // registration-time lookup; mirrors the resolution order of execute()
    public boolean canResolve(String customCheckId, String className) {
        if (customCheckId != null && byId.containsKey(customCheckId)) {
            return true;
        }
        if (className != null) {
            try {
                return CustomCheck.class.isAssignableFrom(Class.forName(className));
            } catch (ClassNotFoundException e) {
                return false;
            }
        }
        return false;
    }

    @Override
    public List<RawFinding> execute(RubricCheck check, ExecutionContext context) {
        String customCheckId = null;
        String className = null;
        if (check.getParametersJson() != null) {
            try {
                JsonNode params = objectMapper.readTree(check.getParametersJson());
                customCheckId = params.path("customCheckId").asText(null);
                className = params.path("className").asText(null);
            } catch (Exception e) {
                log.warn("CUSTOM check {} has invalid parameters JSON: {}", check.getCheckLocalId(), e.getMessage());
            }
        }

        CustomCheck impl = customCheckId != null ? byId.get(customCheckId) : null;
        if (impl == null && className != null) {
            impl = loadReflectively(className);
        }
        if (impl == null) {
            String ref = customCheckId != null ? "customCheckId=" + customCheckId
                    : (className != null ? "className=" + className : "<no customCheckId/className>");
            log.warn("CUSTOM check {} could not resolve a plug-in ({})", check.getCheckLocalId(), ref);
            return List.of(RawFinding.builder()
                    .checkLocalId(check.getCheckLocalId())
                    .dimension(check.getDimension())
                    .severity(Severity.ERROR)
                    .code("custom-check-not-found")
                    .message("No CustomCheck plug-in registered for " + ref)
                    .location(context.getResource().fhirType())
                    .build());
        }

        try {
            return impl.run(check, context);
        } catch (Exception e) {
            log.error("CUSTOM check {} ({}) threw", check.getCheckLocalId(), impl.getClass().getSimpleName(), e);
            return List.of(RawFinding.builder()
                    .checkLocalId(check.getCheckLocalId())
                    .dimension(check.getDimension())
                    .severity(Severity.ERROR)
                    .code("custom-check-error")
                    .message("Custom check threw: " + e.getMessage())
                    .location(context.getResource().fhirType())
                    .build());
        }
    }

    private CustomCheck loadReflectively(String className) {
        try {
            Class<?> clazz = Class.forName(className);
            if (!CustomCheck.class.isAssignableFrom(clazz)) {
                log.warn("Class {} does not implement CustomCheck", className);
                return null;
            }
            CustomCheck cc = (CustomCheck) clazz.getDeclaredConstructor().newInstance();
            byId.putIfAbsent(cc.id(), cc);
            return cc;
        } catch (Exception e) {
            log.warn("Failed to load CustomCheck class {}: {}", className, e.getMessage());
            return null;
        }
    }
}
