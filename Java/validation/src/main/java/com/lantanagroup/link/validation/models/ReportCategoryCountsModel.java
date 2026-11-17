package com.lantanagroup.link.validation.models;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.time.OffsetDateTime;
import java.util.List;

@Data
@NoArgsConstructor
@AllArgsConstructor
public class ReportCategoryCountsModel {
    private String facilityId;
    private String reportId;
    private OffsetDateTime lastUpdated;
    private List<CategoryCountModel> categories;
}
