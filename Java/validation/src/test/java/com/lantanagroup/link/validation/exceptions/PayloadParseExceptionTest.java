package com.lantanagroup.link.validation.exceptions;

import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class PayloadParseExceptionTest {

    @Test
    void carriesTheMessageAndCause() {
        RuntimeException cause = new RuntimeException("underlying parse failure");

        PayloadParseException ex = new PayloadParseException("Malformed JSON payload: duplicate field", cause);

        assertThat(ex.getMessage()).isEqualTo("Malformed JSON payload: duplicate field");
        assertThat(ex.getCause()).isSameAs(cause);
    }
}
