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
import com.lantanagroup.link.shared.utils.LogUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

@Component
public class CustomCheckExecutor implements CheckExecutor {

    private static final Logger logger = LoggerFactory.getLogger(CustomCheckExecutor.class);

    private final ObjectMapper objectMapper;
    private final Map<String, CustomCheck> byId = new ConcurrentHashMap<>();

    public CustomCheckExecutor(List<CustomCheck> customChecks, ObjectMapper objectMapper) {
        this.objectMapper = objectMapper;
        for (CustomCheck cc : customChecks) {
            CustomCheck previous = byId.put(cc.id(), cc);
            if (previous != null) {
                logger.warn("Duplicate CustomCheck id '{}' — {} overrides {}",
                        cc.id(), cc.getClass().getName(), previous.getClass().getName());
            }
        }
        logger.info("Registered {} CustomCheck plug-in(s): {}", byId.size(), byId.keySet());
    }

    @Override
    public CheckType supports() {
        return CheckType.CUSTOM;
    }

    // registration-time lookup; checks resolve only through the Spring-injected registry —
    // rubric-supplied className values must never reach Class.forName or reflective instantiation
    public boolean canResolve(String customCheckId, String className) {
        return customCheckId != null && byId.containsKey(customCheckId);
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
                logger.warn("CUSTOM check {} has invalid parameters JSON: {}",
                        LogUtils.sanitize(check.getCheckLocalId()), LogUtils.sanitize(e.getMessage()));
            }
        }

        CustomCheck impl = customCheckId != null ? byId.get(customCheckId) : null;
        if (impl == null) {
            String ref = customCheckId != null ? "customCheckId=" + customCheckId
                    : (className != null ? "className=" + className : "<no customCheckId/className>");
            logger.warn("CUSTOM check {} could not resolve a plug-in ({})",
                    LogUtils.sanitize(check.getCheckLocalId()), LogUtils.sanitize(ref));
            return List.of(RawFinding.builder()
                    .checkLocalId(check.getCheckLocalId())
                    .dimension(check.getDimension())
                    .severity(Severity.ERROR)
                    .code("custom-check-not-found")
                    .message("No CustomCheck plug-in registered for " + ref)
                    .location(location(context))
                    .build());
        }

        try {
            return impl.run(check, context);
        } catch (Exception e) {
            logger.error("CUSTOM check {} ({}) threw",
                    LogUtils.sanitize(check.getCheckLocalId()), impl.getClass().getSimpleName(), e);
            return List.of(RawFinding.builder()
                    .checkLocalId(check.getCheckLocalId())
                    .dimension(check.getDimension())
                    .severity(Severity.ERROR)
                    .code("custom-check-error")
                    .message("Custom check threw: " + e.getMessage())
                    .location(location(context))
                    .build());
        }
    }

    private static String location(ExecutionContext context) {
        return context.getResource() != null ? context.getResource().fhirType() : null;
    }
}
