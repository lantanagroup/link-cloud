package com.lantanagroup.link.validation.models;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
public class ResultSummaryModel {
    private long count;
    private String severity;
}
