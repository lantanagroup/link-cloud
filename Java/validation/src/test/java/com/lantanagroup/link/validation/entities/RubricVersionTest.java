package com.lantanagroup.link.validation.entities;

import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class RubricVersionTest {

    @Test
    void onCreateStampsCreatedAt() {
        RubricVersion version = RubricVersion.builder().rubricId("piqi.core").semver("1.0.0").build();

        version.onCreate();

        assertThat(version.getCreatedAt()).isNotNull();
    }
}
