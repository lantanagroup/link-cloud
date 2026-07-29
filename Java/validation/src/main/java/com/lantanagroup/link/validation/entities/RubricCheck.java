package com.lantanagroup.link.validation.entities;

import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import jakarta.persistence.*;
import lombok.*;

import java.util.UUID;

@Entity
@Table(
        name = "rubric_check",
        uniqueConstraints = @UniqueConstraint(name = "uq_check_rv_local", columnNames = {"rubric_version_id", "check_local_id"}),
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

    @Column(name = "check_local_id", nullable = false, length = 64)
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

    @Column(nullable = false)
    private int ordinal;

    @Column(nullable = false)
    private boolean enabled;

    @PrePersist
    void onCreate() {
        if (checkId == null) {
            checkId = UUID.randomUUID();
        }
    }
}
