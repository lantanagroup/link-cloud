package com.lantanagroup.link.validation.entities;

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
import org.hl7.fhir.r4.model.OperationOutcome;

import java.util.UUID;

@Entity
@Table(
        name = "legacy_shadow_finding",
        indexes = {
                @Index(name = "ix_legacy_shadow_finding_result", columnList = "result_id"),
                @Index(name = "ix_legacy_shadow_finding_request", columnList = "request_id")
        }
)
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class LegacyShadowFinding {

    @Id
    @Column(name = "finding_id")
    private UUID findingId;

    @Column(name = "result_id", nullable = false)
    private UUID resultId;

    /** Denormalized from the header ({@link LegacyShadowResult#getRequestId()}) so findings are directly
     * joinable to rubric_result by request_id without going through result_id first. */
    @Column(name = "request_id")
    private UUID requestId;

    // Read-only association purely to emit a real FK; resultId above remains the writable mapping.
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "result_id", insertable = false, updatable = false,
            foreignKey = @ForeignKey(name = "fk_legacy_shadow_finding_result"))
    @Getter(AccessLevel.NONE)
    @Setter(AccessLevel.NONE)
    private LegacyShadowResult result;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 16)
    private OperationOutcome.IssueSeverity severity;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 32)
    private OperationOutcome.IssueType code;

    @Lob
    @Column(name = "message", nullable = false)
    private String message;

    @Column(length = 512)
    private String location;

    @Lob
    @Column(name = "expression")
    private String expression;

    /** JSON array of matched category ids -- denormalized audit snapshot, not a live join. */
    @Lob
    @Column(name = "category_ids_json")
    private String categoryIdsJson;

    @Column(name = "acceptable")
    private Boolean acceptable;

    @PrePersist
    void onCreate() {
        if (findingId == null) {
            findingId = UUID.randomUUID();
        }
    }
}
