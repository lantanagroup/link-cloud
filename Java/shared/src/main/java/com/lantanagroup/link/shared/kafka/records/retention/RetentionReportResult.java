package com.lantanagroup.link.shared.kafka.records.retention;

import com.fasterxml.jackson.annotation.JsonInclude;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.time.OffsetDateTime;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
@JsonInclude(JsonInclude.Include.NON_NULL)
public class RetentionReportResult {
    private String reportScheduleId;
    private RetentionResultStatus status;
    private long recordsAffected;

    /**
     * The participant's own description of the failure and the exception type. Never the raw exception
     * message and never PHI; the full exception goes to the participant's logs.
     */
    private String errorMessage;

    private OffsetDateTime completedDateTime;
}
