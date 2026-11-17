package com.lantanagroup.link.validation.services.execution;

import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

class CheckExecutorRegistryTest {

    private static CheckExecutor executorFor(CheckType type) {
        return new CheckExecutor() {
            @Override
            public CheckType supports() {
                return type;
            }

            @Override
            public List<RawFinding> execute(RubricCheck check, ExecutionContext context) {
                return List.of();
            }
        };
    }

    @Test
    @DisplayName("get(type) returns the executor whose supports() matches")
    void resolvesByType() {
        CheckExecutor fhirPath = executorFor(CheckType.FHIRPATH);
        CheckExecutor terminology = executorFor(CheckType.TERMINOLOGY);
        CheckExecutorRegistry registry = new CheckExecutorRegistry(List.of(fhirPath, terminology));

        assertThat(registry.get(CheckType.FHIRPATH)).isSameAs(fhirPath);
        assertThat(registry.get(CheckType.TERMINOLOGY)).isSameAs(terminology);
    }

    @Test
    @DisplayName("get(type) with no registered executor throws IllegalStateException")
    void unregisteredTypeThrows() {
        CheckExecutorRegistry registry = new CheckExecutorRegistry(List.of(executorFor(CheckType.FHIRPATH)));

        assertThatThrownBy(() -> registry.get(CheckType.VALUESET))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("VALUESET");
    }

    @Test
    @DisplayName("an empty executor list resolves nothing")
    void emptyRegistryResolvesNothing() {
        CheckExecutorRegistry registry = new CheckExecutorRegistry(List.of());

        assertThatThrownBy(() -> registry.get(CheckType.FHIRPATH))
                .isInstanceOf(IllegalStateException.class);
    }
}
