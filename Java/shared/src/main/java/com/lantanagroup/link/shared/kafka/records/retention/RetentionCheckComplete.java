package com.lantanagroup.link.shared.kafka.records.retention;

import com.fasterxml.jackson.annotation.JsonInclude;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.time.OffsetDateTime;
import java.util.List;

/**
 * The value of a RetentionCheckComplete message, keyed by retentionCleanupId. A report-scoped participant
 * sets reports; a participant not tied to a report sets summary. See dev-docs/retention-events.md.
 */
@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
@JsonInclude(JsonInclude.Include.NON_NULL)
public class RetentionCheckComplete {
    private String retentionCleanupId;
    private String serviceName;
    private OffsetDateTime completedDateTime;
    private List<RetentionReportResult> reports;
    private RetentionReportResult summary;
}
