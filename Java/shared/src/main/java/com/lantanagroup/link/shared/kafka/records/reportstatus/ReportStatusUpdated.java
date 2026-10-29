package com.lantanagroup.link.shared.kafka.records.reportstatus;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonProperty;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.time.OffsetDateTime;

/**
 * A patient's stage completion or failure, consumed by Report to advance its reporting status.
 * Producers set timestamp to the UTC occurrence time and preserve it when retrying the message.
 * See dev-docs/report-status-events.md.
 */
@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
@JsonInclude(JsonInclude.Include.NON_NULL)
public class ReportStatusUpdated {
    private String reportScheduleId;
    private String patientId;
    private PatientReportingStatus status;
    @JsonProperty("isSuccess")
    private boolean success;
    /** Stage duration in milliseconds, excluding consumer lag. */
    private Long processingTime;
    private String errorMessage;
    private String correlationId;
    /** Populated by Validation on the Validation stage. */
    private ValidationResult validationResult;
    /** Populated by InitialEvaluation; true if any measure is reportable. */
    private Boolean reportable;
    private OffsetDateTime timestamp;
}
