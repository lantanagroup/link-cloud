package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.lantanagroup.link.validation.entities.RubricLifecycleEvent;
import com.lantanagroup.link.validation.enums.RubricLifecycleAction;
import lombok.Builder;
import lombok.Getter;

import java.time.OffsetDateTime;
import java.util.UUID;

@Getter
@Builder
@JsonInclude(JsonInclude.Include.NON_NULL)
public class RubricLifecycleEventDto {

    private UUID eventId;
    private String semver;
    private RubricLifecycleAction action;
    private String actor;
    private String checksum;
    private OffsetDateTime occurredAt;

    public static RubricLifecycleEventDto from(RubricLifecycleEvent event) {
        return RubricLifecycleEventDto.builder()
                .eventId(event.getEventId())
                .semver(event.getSemver())
                .action(event.getAction())
                .actor(event.getActor())
                .checksum(event.getChecksum())
                .occurredAt(event.getOccurredAt())
                .build();
    }
}
