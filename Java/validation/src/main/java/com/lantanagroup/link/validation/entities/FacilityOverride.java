package com.lantanagroup.link.validation.entities;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
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
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.OffsetDateTime;
import java.util.UUID;

@Entity
@Table(
        name = "facility_override",
        uniqueConstraints = @UniqueConstraint(
                name = "uq_fo_facility_rubric_effective",
                columnNames = {"facility_id", "rubric_id", "effective_from"}),
        indexes = {
                @Index(name = "ix_fo_lookup", columnList = "facility_id, rubric_id, effective_from")
        }
)
@Getter
@Setter
@NoArgsConstructor
public class FacilityOverride {

    @Id
    @Column(name = "override_id")
    private UUID overrideId;

    @Column(name = "facility_id", nullable = false, length = 128)
    private String facilityId;

    @Column(name = "rubric_id", nullable = false, length = 128)
    private String rubricId;

    // Read-only association purely to emit a real FK (facility_override.rubric_id -> rubric).
    // The scalar rubricId above remains the writable mapping; do not use this field in code.
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "rubric_id", insertable = false, updatable = false,
            foreignKey = @ForeignKey(name = "fk_facility_override_rubric"))
    @Getter(AccessLevel.NONE)
    @Setter(AccessLevel.NONE)
    private Rubric rubric;

    @Column(name = "rubric_version_id")
    private UUID rubricVersionId;

    @Lob
    @Column(name = "disabled_check_ids_json")
    private String disabledCheckIdsJson;

    @Lob
    @Column(name = "severity_overrides_json")
    private String severityOverridesJson;

    @Lob
    @Column(name = "context_vars_json")
    private String contextVarsJson;

    @Column(name = "effective_from", nullable = false)
    private OffsetDateTime effectiveFrom;

    @Column(name = "effective_to")
    private OffsetDateTime effectiveTo;

    @Column(name = "created_at", nullable = false)
    private OffsetDateTime createdAt;

    @Column(name = "created_by", nullable = false, length = 128)
    private String createdBy;

    // builder on a constructor rather than the class so the read-only rubric association
    // above cannot be set through construction
    @Builder
    private FacilityOverride(UUID overrideId, String facilityId, String rubricId, UUID rubricVersionId,
                             String disabledCheckIdsJson, String severityOverridesJson, String contextVarsJson,
                             OffsetDateTime effectiveFrom, OffsetDateTime effectiveTo,
                             OffsetDateTime createdAt, String createdBy) {
        this.overrideId = overrideId;
        this.facilityId = facilityId;
        this.rubricId = rubricId;
        this.rubricVersionId = rubricVersionId;
        this.disabledCheckIdsJson = disabledCheckIdsJson;
        this.severityOverridesJson = severityOverridesJson;
        this.contextVarsJson = contextVarsJson;
        this.effectiveFrom = effectiveFrom;
        this.effectiveTo = effectiveTo;
        this.createdAt = createdAt;
        this.createdBy = createdBy;
    }

    @PrePersist
    void onCreate() {
        if (overrideId == null) {
            overrideId = UUID.randomUUID();
        }
        if (createdAt == null) {
            createdAt = OffsetDateTime.now();
        }
        if (effectiveFrom == null) {
            effectiveFrom = createdAt;
        }
    }
}
