package com.lantanagroup.link.validation.entities;

import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.ForeignKey;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Index;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.Lob;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.SequenceGenerator;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

// (rubric_version_id, check_local_id) uniqueness applies only to live rows, so the migration
// uses a filtered unique index (uq_check_rv_local_active) rather than @UniqueConstraint,
// which JPA cannot express with a filter.
@Entity
@Table(
        name = "rubric_check",
        indexes = {
                @Index(name = "ix_check_rv_ordinal", columnList = "rubric_version_id, ordinal")
        }
)
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class RubricCheck {

    @Id
    @GeneratedValue(strategy = GenerationType.SEQUENCE, generator = "rubric_check_seq")
    @SequenceGenerator(name = "rubric_check_seq", sequenceName = "rubric_check_sequence", allocationSize = 50)
    @Column(name = "check_id")
    private Long checkId;

    @Column(name = "rubric_version_id", nullable = false)
    private Long rubricVersionId;

    // Read-only association used only to emit the FK
// (rubric_check.rubric_version_id -> rubric_version). The scalar rubricVersionId remains
// the writable mapping; do not use this field in code.
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "rubric_version_id", insertable = false, updatable = false,
            foreignKey = @ForeignKey(name = "fk_check_rubric_version"))
    @Getter(AccessLevel.NONE)
    @Setter(AccessLevel.NONE)
    private RubricVersion rubricVersion;

    @Column(name = "check_local_id", nullable = false, length = 128)
    private String checkLocalId;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 32)
    private CheckType type;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 32)
    private PiqiDimension dimension;

    @Lob
    @Column(name = "parameters_json")
    private String parametersJson;

    @Enumerated(EnumType.STRING)
    @Column(name = "severity_override", length = 16)
    private Severity severityOverride;

    // nullable, checks without an ordinal run first (NULL sorts first in sql server)
    @Column
    private Integer ordinal;

    @Column(nullable = false)
    private boolean enabled;

    // Soft-deleted when a draft re-registration replaces this version's checks.
// Retained for history but excluded from evaluation, dry-runs, and read APIs.
    @Column(nullable = false)
    private boolean deleted;
}
