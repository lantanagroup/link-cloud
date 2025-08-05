package com.lantanagroup.link.validation.models;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
public class CategorySummaryModel {
    private String categoryId;
    private int issues;
    private boolean acceptable;
}
