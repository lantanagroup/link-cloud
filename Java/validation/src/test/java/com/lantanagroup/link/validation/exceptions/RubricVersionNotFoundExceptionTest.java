package com.lantanagroup.link.validation.exceptions;

import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class RubricVersionNotFoundExceptionTest {

    @Test
    void messageNamesTheRubricAndSemver() {
        RubricVersionNotFoundException ex = new RubricVersionNotFoundException("piqi.core", "9.9.9");

        assertThat(ex.getMessage()).isEqualTo("Rubric version not found: piqi.core 9.9.9");
    }
}
