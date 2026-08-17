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
import jakarta.persistence.Id;
import jakarta.persistence.Index;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.Lob;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.PrePersist;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.util.UUID;

// note: (rubric_version_id, check_local_id) uniqueness only applies to live rows, so it's a
// filtered unique index in the migration (uq_check_rv_local_active) rather than a
// @UniqueConstraint here since JPA can't express the filter
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
    @Column(name = "check_id")
    private UUID checkId;

    @Column(name = "rubric_version_id", nullable = false)
    private UUID rubricVersionId;

    // Read-only association purely to emit a real FK (rubric_check.rubric_version_id -> rubric_version).
    // The scalar rubricVersionId above remains the writable mapping; do not use this field in code.
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

    // soft delete, set when a draft re-registration replaces this version's checks.
    // kept for history but hidden from evaluate/dry-run and the read APIs
    @Column(nullable = false)
    private boolean deleted;

    @PrePersist
    void onCreate() {
        if (checkId == null) {
            checkId = UUID.randomUUID();
        }
    }
}
