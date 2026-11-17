package com.lantanagroup.link.shared.kafka.records.retention;

import com.fasterxml.jackson.annotation.JsonProperty;

public enum RetentionResultStatus {
    @JsonProperty("Completed")
    COMPLETED,

    @JsonProperty("Failed")
    FAILED
}
