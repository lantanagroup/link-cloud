package com.lantanagroup.link.validation.entities;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Index;
import jakarta.persistence.Lob;
import jakarta.persistence.PrePersist;
import jakarta.persistence.Table;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.OffsetDateTime;
import java.util.UUID;

/**
 * ADR-0003 shadow-run diff summary: one row per {@code ShadowCompareEvent} processed, recording whether the
 * two engines agreed and, if not, how. No FK to {@link LegacyShadowResult}/{@code RubricResult} -- which one
 * applies depends on {@link #ranNewEngine}, so the two are simply joinable by {@code reportId}/{@code correlationId}.
 */
@Entity
@Table(
        name = "shadow_comparison_result",
        indexes = {
                @Index(name = "ix_shadow_comparison_facility_report", columnList = "facility_id, report_id"),
                @Index(name = "ix_shadow_comparison_compared_at", columnList = "compared_at"),
                @Index(name = "ix_shadow_comparison_request", columnList = "request_id")
        }
)
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class ShadowComparisonResult {

    @Id
    @Column(name = "id")
    private UUID id;

    /** Same id as the corresponding rubric_result row, when the rubric engine was primary; null when
     * legacy was primary (that direction has no rubric request to share). See {@link
     * com.lantanagroup.link.validation.records.ShadowCompareEvent#getRequestId()}. Indexed, not unique --
     * multiple comparison rows could in principle share it (e.g. a redelivered Kafka message). */
    @Column(name = "request_id")
    private UUID requestId;

    @Column(name = "correlation_id", length = 128)
    private String correlationId;

    @Column(name = "facility_id", length = 128, nullable = false)
    private String facilityId;

    @Column(name = "patient_id", length = 128, nullable = false)
    private String patientId;

    @Column(name = "report_id", length = 128, nullable = false)
    private String reportId;

    @Column(name = "rubric_id", length = 128)
    private String rubricId;

    /** Which engine the primary already ran; the shadow consumer ran the other one. */
    @Column(name = "ran_new_engine", nullable = false)
    private boolean ranNewEngine;

    @Column(name = "matched", nullable = false)
    private boolean matched;

    @Column(name = "added_count", nullable = false)
    private int addedCount;

    @Column(name = "missing_count", nullable = false)
    private int missingCount;

    @Column(name = "severity_changed_count", nullable = false)
    private int severityChangedCount;

    @Column(name = "matched_finding_count", nullable = false)
    private int matchedFindingCount;

    /** Findings present only in the new engine's output, as a JSON array -- null when there are none. */
    @Lob
    @Column(name = "added_json")
    private String addedJson;

    /** Findings present only in the legacy engine's output, as a JSON array -- null when there are none. */
    @Lob
    @Column(name = "missing_json")
    private String missingJson;

    /** Findings matched by key but disagreeing on severity/message, as a JSON array of legacy/modern pairs
     * -- null when there are none. */
    @Lob
    @Column(name = "severity_changed_json")
    private String severityChangedJson;

    @Column(name = "compared_at", nullable = false)
    private OffsetDateTime comparedAt;

    @PrePersist
    void onCreate() {
        if (id == null) {
            id = UUID.randomUUID();
        }
    }
}
