package com.lantanagroup.link.validation.entities;

import jakarta.persistence.*;
import lombok.Getter;
import lombok.Setter;

/**
 * A report's total of validation findings in one category. It is kept after the report's findings are deleted, so
 * the category's title, acceptable and submit flags are captured when the count is written rather than read later.
 */
@Getter
@Setter
@Entity
@Table(uniqueConstraints = @UniqueConstraint(
        name = "uq_report_category_count_facility_id_report_id_category_id",
        columnNames = {"facility_id", "report_id", "category_id"}))
public class ReportCategoryCount {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private String facilityId;

    @Column(nullable = false)
    private String reportId;

    @Column(nullable = false)
    private String categoryId;

    @Column(nullable = false)
    private String title;

    @Column(nullable = false)
    private boolean acceptable;

    @Column(nullable = false)
    private boolean submit;

    @Column(nullable = false)
    private long issues;
}
