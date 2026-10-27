package com.lantanagroup.link.validation.entities;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Index;
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
 * ADR-0003 shadow-run audit record: one row per legacy-engine run performed only for comparison (never
 * authoritative), mirroring what {@link RubricResult} already records for the modern engine.
 */
@Entity
@Table(
        name = "legacy_shadow_result",
        indexes = {
                @Index(name = "ix_legacy_shadow_result_facility_report", columnList = "facility_id, report_id"),
                @Index(name = "ix_legacy_shadow_result_correlation", columnList = "correlation_id"),
                @Index(name = "ix_legacy_shadow_result_request", columnList = "request_id")
        }
)
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class LegacyShadowResult {

    @Id
    @Column(name = "result_id")
    private UUID resultId;

    /** Same id as the corresponding rubric_result row, when the rubric engine was primary and this legacy
     * run was only for comparison; null when legacy was primary (that direction has no rubric request to
     * share). */
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

    @Column(name = "fatal_count", nullable = false)
    private int fatalCount;

    @Column(name = "error_count", nullable = false)
    private int errorCount;

    @Column(name = "warning_count", nullable = false)
    private int warningCount;

    @Column(name = "information_count", nullable = false)
    private int informationCount;

    @Column(name = "requested_at", nullable = false)
    private OffsetDateTime requestedAt;

    @Column(name = "completed_at", nullable = false)
    private OffsetDateTime completedAt;

    @Column(name = "duration_ms", nullable = false)
    private long durationMs;

    @PrePersist
    void onCreate() {
        if (resultId == null) {
            resultId = UUID.randomUUID();
        }
    }
}
