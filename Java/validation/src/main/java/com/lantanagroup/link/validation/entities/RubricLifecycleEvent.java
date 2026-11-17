package com.lantanagroup.link.validation.entities;

import com.lantanagroup.link.validation.enums.RubricLifecycleAction;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.ForeignKey;
import jakarta.persistence.Id;
import jakarta.persistence.Index;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.PrePersist;
import jakarta.persistence.Table;
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
        name = "rubric_lifecycle_event",
        indexes = @Index(name = "ix_rle_rubric", columnList = "rubric_id, occurred_at")
)
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class RubricLifecycleEvent {

    @Id
    @Column(name = "event_id")
    private UUID eventId;

    @Column(name = "rubric_id", length = 128, nullable = false)
    private String rubricId;

    // Read-only association purely to emit a real FK (rubric_lifecycle_event.rubric_id -> rubric).
    // The scalar rubricId above remains the writable mapping; do not use this field in code.
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "rubric_id", insertable = false, updatable = false,
            foreignKey = @ForeignKey(name = "fk_lifecycle_event_rubric"))
    @Getter(AccessLevel.NONE)
    @Setter(AccessLevel.NONE)
    private Rubric rubric;

    @Column(nullable = false, length = 32)
    private String semver;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 16)
    private RubricLifecycleAction action;

    @Column(length = 128)
    private String actor;

    @Column(length = 64)
    private String checksum;

    @Column(name = "occurred_at", nullable = false)
    private OffsetDateTime occurredAt;

    @PrePersist
    void onCreate() {
        if (eventId == null) {
            eventId = UUID.randomUUID();
        }
        if (occurredAt == null) {
            occurredAt = OffsetDateTime.now();
        }
    }
}
