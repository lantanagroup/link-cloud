package com.lantanagroup.link.validation.entities;

import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;

import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;

class LegacyShadowFindingTest {

    @Test
    void onCreateGeneratesFindingIdWhenAbsent() {
        LegacyShadowFinding finding = LegacyShadowFinding.builder()
                .resultId(UUID.randomUUID())
                .severity(OperationOutcome.IssueSeverity.WARNING)
                .message("m")
                .build();

        finding.onCreate();

        assertThat(finding.getFindingId()).isNotNull();
    }

    @Test
    void onCreatePreservesAnAlreadySetFindingId() {
        UUID fixedId = UUID.randomUUID();
        LegacyShadowFinding finding = LegacyShadowFinding.builder()
                .findingId(fixedId)
                .resultId(UUID.randomUUID())
                .severity(OperationOutcome.IssueSeverity.WARNING)
                .message("m")
                .build();

        finding.onCreate();

        assertThat(finding.getFindingId()).isEqualTo(fixedId);
    }
}
