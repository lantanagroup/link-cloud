package com.lantanagroup.link.validation.services.execution.executors;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.services.execution.spi.CustomCheck;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class CustomCheckExecutorTest {

    private final CustomCheckExecutor executor = new CustomCheckExecutor(List.of(), new ObjectMapper());

    private static RubricCheck check(String parametersJson) {
        return RubricCheck.builder()
                .checkLocalId("cc-1")
                .dimension(PiqiDimension.CONFORMANCE)
                .parametersJson(parametersJson)
                .build();
    }

    private static class StubCheck implements CustomCheck {
        @Override
        public String id() {
            return "stub";
        }

        @Override
        public List<RawFinding> run(RubricCheck check, ExecutionContext context) {
            return List.of(RawFinding.builder().code("stub-ran").build());
        }
    }

    private static class ThrowingCheck implements CustomCheck {
        @Override
        public String id() {
            return "throwing";
        }

        @Override
        public List<RawFinding> run(RubricCheck check, ExecutionContext context) {
            throw new IllegalStateException("boom");
        }
    }

    // resolved reflectively by class name below; needs a public no-arg constructor
    public static class ReflectiveCheck implements CustomCheck {
        @Override
        public String id() {
            return "reflective";
        }

        @Override
        public List<RawFinding> run(RubricCheck check, ExecutionContext context) {
            return List.of(RawFinding.builder().code("reflective-ran").build());
        }
    }

    @Test
    @DisplayName("unresolved plug-in with a null resource -> error finding with null location, no NPE")
    void unresolvedPluginWithNullResource() {
        List<RawFinding> findings = executor.execute(
                check("{\"customCheckId\":\"does-not-exist\"}"), new ExecutionContext());

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("custom-check-not-found");
        assertThat(findings.get(0).getSeverity()).isEqualTo(Severity.ERROR);
        assertThat(findings.get(0).getLocation()).isNull();
    }

    @Test
    @DisplayName("registered plug-in resolved by id runs and returns its findings")
    void registeredPluginRuns() {
        CustomCheckExecutor withStub = new CustomCheckExecutor(List.of(new StubCheck()), new ObjectMapper());

        List<RawFinding> findings = withStub.execute(
                check("{\"customCheckId\":\"stub\"}"), new ExecutionContext());

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("stub-ran");
    }

    @Test
    @DisplayName("malformed parameters JSON falls through to a not-found finding")
    void malformedParametersJson() {
        List<RawFinding> findings = executor.execute(check("{not json"), new ExecutionContext());

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("custom-check-not-found");
    }

    @Test
    @DisplayName("plug-in resolved reflectively by class name runs and returns its findings")
    void reflectiveResolutionByClassName() {
        List<RawFinding> findings = executor.execute(
                check("{\"className\":\"" + ReflectiveCheck.class.getName() + "\"}"), new ExecutionContext());

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("reflective-ran");
    }

    @Test
    @DisplayName("plug-in throwing at run time yields an error finding instead of propagating")
    void pluginExceptionYieldsErrorFinding() {
        CustomCheckExecutor withThrowing = new CustomCheckExecutor(List.of(new ThrowingCheck()), new ObjectMapper());

        List<RawFinding> findings = withThrowing.execute(
                check("{\"customCheckId\":\"throwing\"}"), new ExecutionContext());

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("custom-check-error");
        assertThat(findings.get(0).getSeverity()).isEqualTo(Severity.ERROR);
        assertThat(findings.get(0).getMessage()).contains("boom");
    }
}
