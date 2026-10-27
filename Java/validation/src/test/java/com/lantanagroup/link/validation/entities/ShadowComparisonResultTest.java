package com.lantanagroup.link.validation.entities;

import org.junit.jupiter.api.Test;

import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;

class ShadowComparisonResultTest {

    @Test
    void onCreateGeneratesIdWhenAbsent() {
        ShadowComparisonResult result = ShadowComparisonResult.builder()
                .facilityId("f1").patientId("p1").reportId("r1").build();

        result.onCreate();

        assertThat(result.getId()).isNotNull();
    }

    @Test
    void onCreatePreservesAnAlreadySetId() {
        UUID fixedId = UUID.randomUUID();
        ShadowComparisonResult result = ShadowComparisonResult.builder()
                .id(fixedId).facilityId("f1").patientId("p1").reportId("r1").build();

        result.onCreate();

        assertThat(result.getId()).isEqualTo(fixedId);
    }
}
