package com.lantanagroup.link.validation.models;

import com.lantanagroup.link.validation.entities.RubricLifecycleEvent;
import com.lantanagroup.link.validation.enums.RubricLifecycleAction;
import org.junit.jupiter.api.Test;

import java.time.OffsetDateTime;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;

class RubricLifecycleEventDtoTest {

    @Test
    void fromCopiesEveryFieldFromTheEntity() {
        UUID eventId = UUID.randomUUID();
        OffsetDateTime occurredAt = OffsetDateTime.now();
        RubricLifecycleEvent event = RubricLifecycleEvent.builder()
                .eventId(eventId)
                .rubricId("piqi.core")
                .semver("1.0.0")
                .action(RubricLifecycleAction.PUBLISHED)
                .actor("qa")
                .checksum("abc123")
                .occurredAt(occurredAt)
                .build();

        RubricLifecycleEventDto dto = RubricLifecycleEventDto.from(event);

        assertThat(dto.getEventId()).isEqualTo(eventId);
        assertThat(dto.getSemver()).isEqualTo("1.0.0");
        assertThat(dto.getAction()).isEqualTo(RubricLifecycleAction.PUBLISHED);
        assertThat(dto.getActor()).isEqualTo("qa");
        assertThat(dto.getChecksum()).isEqualTo("abc123");
        assertThat(dto.getOccurredAt()).isEqualTo(occurredAt);
    }
}
