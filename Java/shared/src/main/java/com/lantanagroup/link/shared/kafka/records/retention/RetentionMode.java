package com.lantanagroup.link.shared.kafka.records.retention;

import com.fasterxml.jackson.annotation.JsonProperty;

public enum RetentionMode {
    @JsonProperty("Scrub")
    SCRUB,

    @JsonProperty("Purge")
    PURGE
}
