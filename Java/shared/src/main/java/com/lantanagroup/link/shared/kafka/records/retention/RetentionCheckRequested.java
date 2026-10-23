package com.lantanagroup.link.shared.kafka.records.retention;

import com.fasterxml.jackson.annotation.JsonInclude;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.time.OffsetDateTime;
import java.util.List;

/**
 * The value of a RetentionCheckRequested message, keyed by retentionCleanupId.
 * See dev-docs/retention-events.md for the contract and the rules a participant follows.
 */
@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
@JsonInclude(JsonInclude.Include.NON_NULL)
public class RetentionCheckRequested {
    private String retentionCleanupId;
    private RetentionInitiatedBy initiatedBy;
    private OffsetDateTime initiatedDateTime;
    private RetentionMode mode;
    private OffsetDateTime cutoffDateTime;
    private List<RetentionReport> reports;
    private List<String> participants;
}
