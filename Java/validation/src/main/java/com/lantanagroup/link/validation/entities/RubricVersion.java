package com.lantanagroup.link.validation.entities;

import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.ForeignKey;
import jakarta.persistence.Id;
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
        name = "rubric_version",
        uniqueConstraints = @UniqueConstraint(name = "uq_rv_rubric_semver", columnNames = {"rubric_id", "semver"})
)
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class RubricVersion {

    @Id
    @Column(name = "rubric_version_id")
    private UUID rubricVersionId;

    @Column(name = "rubric_id", length = 128, nullable = false)
    private String rubricId;

    // Read-only association purely to emit a real FK (rubric_version.rubric_id -> rubric).
    // The scalar rubricId above remains the writable mapping; do not use this field in code.
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "rubric_id", insertable = false, updatable = false,
            foreignKey = @ForeignKey(name = "fk_rubric_version_rubric"))
    @Getter(AccessLevel.NONE)
    @Setter(AccessLevel.NONE)
    private Rubric rubric;

    @Column(nullable = false, length = 32)
    private String semver;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 16)
    private RubricVersionStatus status;

    @Column(name = "published_at")
    private OffsetDateTime publishedAt;

    @Column(name = "published_by", length = 128)
    private String publishedBy;

    @Column(name = "retired_at")
    private OffsetDateTime retiredAt;

    @Column(name = "retired_by", length = 128)
    private String retiredBy;

    // Dry-run gate: written when a $dry-run completes for this version, read at publish time
    // when link.rubric.dry-run.required-for-publish is enabled. Null = no dry run completed.
    @Column(name = "dry_run_completed_at")
    private OffsetDateTime dryRunCompletedAt;

    @Enumerated(EnumType.STRING)
    @Column(name = "dry_run_status", length = 32)
    private RubricResultStatus dryRunStatus;

    @Column(length = 64, nullable = false)
    private String checksum;

    // null for rows created before this column existed
    @Lob
    @Column(name = "definition_json")
    private String definitionJson;

    // Per-version declarative metadata (declared PIQI dimensions, applicable context, scoring policy).
    // These live on the version — not the rubric — because they are declared per version and can change
    // between versions.
    @Lob
    @Column(name = "dimensions_json")
    private String dimensionsJson;

    @Lob
    @Column(name = "applicable_context_json")
    private String applicableContextJson;

    @Lob
    @Column(name = "scoring_policy_json")
    private String scoringPolicyJson;

    @Column(name = "created_at", nullable = false)
    private OffsetDateTime createdAt;

    @Column(name = "created_by", length = 128)
    private String createdBy;

    @PrePersist
    void onCreate() {
        if (rubricVersionId == null) {
            rubricVersionId = UUID.randomUUID();
        }
        createdAt = OffsetDateTime.now();
    }
}
