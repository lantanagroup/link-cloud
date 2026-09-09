package com.lantanagroup.link.validation.exceptions;

import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class RubricVersionConflictExceptionTest {

    @Test
    void twoArgConstructor_messageDescribesADifferentDefinitionConflict() {
        RubricVersionConflictException ex = new RubricVersionConflictException("piqi.core", "1.0.0");

        assertThat(ex.getMessage())
                .contains("piqi.core")
                .contains("1.0.0")
                .contains("already registered with a different definition");
    }

    @Test
    void threeArgConstructor_messageNamesTheImmutableStatus() {
        RubricVersionConflictException ex =
                new RubricVersionConflictException("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED);

        assertThat(ex.getMessage())
                .contains("piqi.core")
                .contains("1.0.0")
                .contains("PUBLISHED")
                .contains("definition is immutable");
    }
}
