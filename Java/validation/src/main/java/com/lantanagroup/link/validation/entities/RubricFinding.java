package com.lantanagroup.link.validation.entities;

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

@Entity
@Table(
        name = "rubric_finding",
        indexes = {
                @Index(name = "ix_finding_result", columnList = "result_id"),
                @Index(name = "ix_finding_check", columnList = "check_id"),
                @Index(name = "ix_finding_severity", columnList = "result_id, severity")
        }
)
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class RubricFinding {

    @Id
    @Column(name = "finding_id")
    private UUID findingId;

    @Column(name = "result_id", nullable = false)
    private UUID resultId;

    @Column(name = "check_id", nullable = false)
    private UUID checkId;

    // Read-only associations purely to emit real FKs; the scalar id columns above remain the writable
    // mappings. Do not use these fields in code.
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "result_id", insertable = false, updatable = false,
            foreignKey = @ForeignKey(name = "fk_finding_result"))
    @Getter(AccessLevel.NONE)
    @Setter(AccessLevel.NONE)
    private RubricResult result;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "check_id", insertable = false, updatable = false,
            foreignKey = @ForeignKey(name = "fk_finding_check"))
    @Getter(AccessLevel.NONE)
    @Setter(AccessLevel.NONE)
    private RubricCheck check;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 32)
    private PiqiDimension dimension;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 16)
    private Severity severity;

    @Column(nullable = false, length = 128)
    private String code;

    @Lob
    @Column(name = "message", nullable = false)
    private String message;

    @Column(length = 512)
    private String location;

    @Lob
    @Column(name = "expression")
    private String expression;

    @PrePersist
    void onCreate() {
        if (findingId == null) {
            findingId = UUID.randomUUID();
        }
    }
}
