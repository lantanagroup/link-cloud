package com.lantanagroup.link.validation.exceptions;

import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class RubricDryRunRequiredExceptionTest {

    @Test
    void messageNamesTheRubricAndVersion() {
        RubricDryRunRequiredException ex = new RubricDryRunRequiredException("piqi.core", "1.0.0");

        assertThat(ex.getMessage())
                .contains("piqi.core")
                .contains("1.0.0")
                .contains("no dry run has been completed");
    }
}
