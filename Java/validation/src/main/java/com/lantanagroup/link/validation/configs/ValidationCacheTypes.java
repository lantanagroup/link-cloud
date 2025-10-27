package com.lantanagroup.link.validation.configs;

import com.fasterxml.jackson.annotation.JsonProperty;

public enum ValidationCacheTypes {
    @JsonProperty("none")
    NONE,
    @JsonProperty("memory")
    MEMORY,
    @JsonProperty("redis")
    REDIS
}
