package com.lantanagroup.link.shared.kafka.records.reportstatus;

import com.fasterxml.jackson.annotation.JsonPropertyOrder;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

/**
 * Groups status updates for one patient within one report. reportId is the value's reportScheduleId.
 * Property order is part of the wire contract because Kafka partitions by serialized key bytes.
 */
@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
@JsonPropertyOrder({"reportId", "patientId"})
public class ReportStatusUpdatedKey {
    private String reportId;
    private String patientId;
}
