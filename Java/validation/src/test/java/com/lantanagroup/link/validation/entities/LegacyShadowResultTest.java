package com.lantanagroup.link.validation.entities;

import org.junit.jupiter.api.Test;

import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;

class LegacyShadowResultTest {

    @Test
    void onCreateGeneratesResultIdWhenAbsent() {
        LegacyShadowResult result = LegacyShadowResult.builder()
                .facilityId("f1").patientId("p1").reportId("r1").build();

        result.onCreate();

        assertThat(result.getResultId()).isNotNull();
    }

    @Test
    void onCreatePreservesAnAlreadySetResultId() {
        UUID fixedId = UUID.randomUUID();
        LegacyShadowResult result = LegacyShadowResult.builder()
                .resultId(fixedId).facilityId("f1").patientId("p1").reportId("r1").build();

        result.onCreate();

        assertThat(result.getResultId()).isEqualTo(fixedId);
    }
}
