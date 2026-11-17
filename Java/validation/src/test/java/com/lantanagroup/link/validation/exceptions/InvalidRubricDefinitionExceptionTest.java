package com.lantanagroup.link.validation.exceptions;

import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class InvalidRubricDefinitionExceptionTest {

    @Test
    void carriesTheMessageAndErrorListVerbatim() {
        List<String> errors = List.of("checks[c1]: FHIRPATH requires parameters.expression");

        InvalidRubricDefinitionException ex = new InvalidRubricDefinitionException("Invalid rubric definition", errors);

        assertThat(ex.getMessage()).isEqualTo("Invalid rubric definition");
        assertThat(ex.getErrors()).isEqualTo(errors);
    }

    @Test
    void errorsCanBeNull() {
        InvalidRubricDefinitionException ex = new InvalidRubricDefinitionException("Invalid rubric definition", null);

        assertThat(ex.getErrors()).isNull();
    }
}
