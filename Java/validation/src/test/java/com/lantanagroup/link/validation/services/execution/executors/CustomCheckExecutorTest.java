package com.lantanagroup.link.validation.services.execution.executors;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class CustomCheckExecutorTest {

    private final CustomCheckExecutor executor = new CustomCheckExecutor(List.of(), new ObjectMapper());

    @Test
    @DisplayName("unresolved plug-in with a null resource -> error finding with null location, no NPE")
    void unresolvedPluginWithNullResource() {
        RubricCheck check = RubricCheck.builder()
                .checkLocalId("cc-1")
                .dimension(PiqiDimension.CONFORMANCE)
                .parametersJson("{\"customCheckId\":\"does-not-exist\"}")
                .build();

        List<RawFinding> findings = executor.execute(check, new ExecutionContext());

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("custom-check-not-found");
        assertThat(findings.get(0).getSeverity()).isEqualTo(Severity.ERROR);
        assertThat(findings.get(0).getLocation()).isNull();
    }
}
