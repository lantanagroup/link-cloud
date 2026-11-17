package com.lantanagroup.link.validation.entities;

import com.lantanagroup.link.validation.enums.RubricLifecycleAction;
import org.junit.jupiter.api.Test;

import java.time.OffsetDateTime;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;

class RubricLifecycleEventTest {

    @Test
    void onCreateGeneratesEventIdAndOccurredAtWhenAbsent() {
        RubricLifecycleEvent event = RubricLifecycleEvent.builder()
                .rubricId("piqi.core").semver("1.0.0").action(RubricLifecycleAction.REGISTERED).build();

        event.onCreate();

        assertThat(event.getEventId()).isNotNull();
        assertThat(event.getOccurredAt()).isNotNull();
    }

    @Test
    void onCreatePreservesAnAlreadySetEventIdAndOccurredAt() {
        UUID fixedId = UUID.randomUUID();
        OffsetDateTime fixedTime = OffsetDateTime.parse("2026-01-01T00:00:00Z");
        RubricLifecycleEvent event = RubricLifecycleEvent.builder()
                .eventId(fixedId).occurredAt(fixedTime)
                .rubricId("piqi.core").semver("1.0.0").action(RubricLifecycleAction.PUBLISHED).build();

        event.onCreate();

        assertThat(event.getEventId()).isEqualTo(fixedId);
        assertThat(event.getOccurredAt()).isEqualTo(fixedTime);
    }
}
