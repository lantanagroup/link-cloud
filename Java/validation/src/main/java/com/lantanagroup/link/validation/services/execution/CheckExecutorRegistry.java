package com.lantanagroup.link.validation.services.execution;

import com.lantanagroup.link.validation.enums.CheckType;
import org.springframework.stereotype.Component;

import java.util.EnumMap;
import java.util.List;
import java.util.Map;

@Component
public class CheckExecutorRegistry {

    private final Map<CheckType, CheckExecutor> byType = new EnumMap<>(CheckType.class);

    public CheckExecutorRegistry(List<CheckExecutor> executors) {
        for (CheckExecutor e : executors) {
            byType.put(e.supports(), e);
        }
    }

    public CheckExecutor get(CheckType type) {
        CheckExecutor executor = byType.get(type);
        if (executor == null) {
            throw new IllegalStateException("No CheckExecutor registered for type: " + type);
        }
        return executor;
    }
}
