package com.lantanagroup.link.shared.kafka.records.retention;

import com.fasterxml.jackson.annotation.JsonProperty;

/**
 * Wire names are pinned with @JsonProperty so they match the .NET enum names exactly, as every retention
 * enum does.
 */
public enum RetentionInitiatedBy {
    @JsonProperty("Scheduled")
    SCHEDULED,

    @JsonProperty("Manual")
    MANUAL,

    @JsonProperty("SingleReport")
    SINGLE_REPORT
}
