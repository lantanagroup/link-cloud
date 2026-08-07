package com.lantanagroup.link.validation.entities;

import com.lantanagroup.link.validation.enums.RubricResultStatus;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.ForeignKey;
import jakarta.persistence.Id;
import jakarta.persistence.Index;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.Lob;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.PrePersist;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import lombok.AccessLevel;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.OffsetDateTime;
import java.util.UUID;

@Entity
@Table(
        name = "rubric_result",
        uniqueConstraints = @UniqueConstraint(name = "uq_result_request", columnNames = "request_id"),
        indexes = {
                @Index(name = "ix_result_rubric", columnList = "rubric_id, completed_at"),
                @Index(name = "ix_result_facility_report", columnList = "facility_id, report_id"),
                @Index(name = "ix_result_status", columnList = "status, completed_at"),
                @Index(name = "ix_result_workflow", columnList = "workflow_tag, completed_at")
        }
)
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class RubricResult {

    @Id
    @Column(name = "result_id")
    private UUID resultId;

    @Column(name = "request_id", nullable = false)
    private UUID requestId;

    @Column(name = "rubric_id", nullable = false, length = 128)
    private String rubricId;

    @Column(name = "rubric_version_id", nullable = false)
    private UUID rubricVersionId;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "rubric_id", insertable = false, updatable = false,
            foreignKey = @ForeignKey(name = "fk_result_rubric"))
    @Getter(AccessLevel.NONE)
    @Setter(AccessLevel.NONE)
    private Rubric rubric;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "rubric_version_id", insertable = false, updatable = false,
            foreignKey = @ForeignKey(name = "fk_result_rubric_version"))
    @Getter(AccessLevel.NONE)
    @Setter(AccessLevel.NONE)
    private RubricVersion rubricVersion;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 32)
    private RubricResultStatus status;

    @Lob
    @Column(name = "score_json")
    private String scoreJson;

    @Column(name = "error_count", nullable = false)
    private int errorCount;

    @Column(name = "warning_count", nullable = false)
    private int warningCount;

    @Column(name = "information_count", nullable = false)
    private int informationCount;

    @Lob
    @Column(name = "supporting_artifacts_json")
    private String supportingArtifactsJson;

    @Column(name = "correlation_id", length = 128)
    private String correlationId;

    @Column(length = 128)
    private String requestor;

    @Column(name = "payload_ref", length = 512)
    private String payloadRef;

    @Column(name = "facility_id", length = 128)
    private String facilityId;

    @Column(name = "patient_id", length = 128)
    private String patientId;

    @Column(name = "report_id", length = 128)
    private String reportId;

    @Column(name = "workflow_tag", length = 128)
    private String workflowTag;

    @Column(length = 64)
    private String stage;

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
