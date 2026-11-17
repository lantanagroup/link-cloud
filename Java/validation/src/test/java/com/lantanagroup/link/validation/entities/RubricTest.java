package com.lantanagroup.link.validation.entities;

import org.junit.jupiter.api.Test;

import java.time.OffsetDateTime;

import static org.assertj.core.api.Assertions.assertThat;

class RubricTest {

    @Test
    void onCreateStampsBothCreatedAtAndUpdatedAtToNow() {
        Rubric rubric = Rubric.builder().rubricId("piqi.core").title("PIQI Core").build();

        rubric.onCreate();

        assertThat(rubric.getCreatedAt()).isNotNull();
        assertThat(rubric.getUpdatedAt()).isNotNull();
        assertThat(rubric.getCreatedAt()).isEqualTo(rubric.getUpdatedAt());
    }

    @Test
    void onUpdateTouchesOnlyUpdatedAt() {
        Rubric rubric = Rubric.builder().rubricId("piqi.core").title("PIQI Core").build();
        rubric.onCreate();
        OffsetDateTime originalCreatedAt = rubric.getCreatedAt();
        OffsetDateTime originalUpdatedAt = rubric.getUpdatedAt();

        // Force a detectable time difference regardless of clock resolution.
        rubric.setCreatedAt(originalCreatedAt.minusSeconds(5));
        rubric.onUpdate();

        assertThat(rubric.getCreatedAt()).isEqualTo(originalCreatedAt.minusSeconds(5));
        assertThat(rubric.getUpdatedAt()).isAfterOrEqualTo(originalUpdatedAt);
    }
}
