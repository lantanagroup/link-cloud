package com.lantanagroup.link.shared.kafka.records.reportstatus;

import com.fasterxml.jackson.annotation.JsonProperty;

/** Report lifecycle derived from patient reporting statuses; never used as an event status. */
public enum ReportStatus {
    @JsonProperty("Pending")
    PENDING,

    @JsonProperty("Completed")
    COMPLETED,

    @JsonProperty("CompletedNotSubmitted")
    COMPLETED_NOT_SUBMITTED,

    @JsonProperty("Failure")
    FAILURE,

    @JsonProperty("Canceled")
    CANCELED
}

