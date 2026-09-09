package com.lantanagroup.link.validation.exceptions;

import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class RubricNotFoundExceptionTest {

    @Test
    void messageNamesTheRubricId() {
        RubricNotFoundException ex = new RubricNotFoundException("piqi.core");

        assertThat(ex.getMessage()).isEqualTo("Rubric not found: piqi.core");
    }
}
