package com.lantanagroup.link.validation.models;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
public class CategoryIssueModel {
    private String location;
    private String message;
    private String code;
    private String expression;
    private String patientId;
}