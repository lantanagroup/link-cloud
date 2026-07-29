package com.lantanagroup.link.validation.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import lombok.*;

import java.time.OffsetDateTime;
import java.util.Map;

@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
@JsonInclude(JsonInclude.Include.NON_NULL)
public class TraceDto {
    private OffsetDateTime requestedAt;
    private OffsetDateTime completedAt;
    private long durationMs;
    private Map<String, Long> checkDurationsMs;
    private String validatorVersion;
}
