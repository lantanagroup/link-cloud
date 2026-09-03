package com.lantanagroup.link.validation.records;

import com.lantanagroup.link.shared.kafka.Topics;
import lombok.Getter;
import lombok.Setter;

import java.util.List;
import java.util.UUID;

/** ADR-0003 shadow-run: published alongside {@code ValidationComplete} so {@code ShadowValidationConsumer} can run the other engine and compare. */
@Getter
@Setter
public class ShadowCompareEvent {
    public static final String TOPIC = Topics.SHADOW_COMPARE_EVENT;

    private String correlationId;
    private String facilityId;
    private String patientId;
    private String reportId;
    private String payloadUri;

    /** Which engine the primary consumer already ran (and whose output is in {@link #authoritativeResult}). */
    private boolean ranNewEngine;

    /** Set only when {@link #ranNewEngine} is true -- the rubric engine's own request id (already
     * generated for its rubric_result row), so the legacy run this event triggers can be stamped with the
     * same id. Null when the legacy engine was primary; that direction has no rubric request to share. */
    private UUID requestId;

    private List<ShadowFindingDto> authoritativeResult;
}
