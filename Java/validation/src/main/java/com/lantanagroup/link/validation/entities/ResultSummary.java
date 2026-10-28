package com.lantanagroup.link.validation.entities;

import lombok.Getter;
import org.apache.commons.lang3.StringUtils;

import java.util.Map;

@Getter
public class ResultSummary {
    private String value;
    private long count;

    public ResultSummary() {
    }

    public ResultSummary(String value, long count) {
        if (StringUtils.isEmpty(value)) {
            throw new IllegalArgumentException("No value specified");
        }
        if (count < 0) {
            throw new IllegalArgumentException("Count must be positive");
        }
        this.value = value;
        this.count = count;
    }

    public ResultSummary(Map.Entry<String, Long> entry) {
        this(entry.getKey(), entry.getValue());
    }
}
