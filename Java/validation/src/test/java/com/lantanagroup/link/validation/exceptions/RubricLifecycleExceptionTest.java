package com.lantanagroup.link.validation.exceptions;

import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class RubricLifecycleExceptionTest {

    @Test
    void messageNamesTheRubricVersionAndCurrentStatus() {
        RubricLifecycleException ex =
                new RubricLifecycleException("piqi.core", "1.0.0", RubricVersionStatus.PUBLISHED, "publish");

        assertThat(ex.getMessage()).isEqualTo("Cannot publish piqi.core v1.0.0: status is PUBLISHED");
    }
}
