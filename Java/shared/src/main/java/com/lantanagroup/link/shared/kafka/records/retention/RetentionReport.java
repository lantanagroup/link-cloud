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
public class RetentionReport {
    private String facilityId;
    private String reportScheduleId;
    private RetentionWindow retentionWindow;
    private OffsetDateTime cutoffDateTime;
}
