package com.lantanagroup.link.validation.models;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
public class CategoryCountModel {
    private String categoryId;
    private String title;
    private boolean acceptable;
    private boolean submit;
    private long issues;
}
