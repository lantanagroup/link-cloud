package com.lantanagroup.link.validation.entities;

import jakarta.persistence.*;
import lombok.Getter;
import lombok.Setter;

/**
 * One patient's contribution to a report's category totals. A patient can be validated more than once for the same
 * report (a redelivered message, for example), so these rows let a repeat replace its earlier counts instead of
 * adding to them. They are only needed while the patient's findings exist, and are deleted with them.
 * See dev-docs/validation-category-counts.md.
 */
@Getter
@Setter
@Entity
@Table(uniqueConstraints = @UniqueConstraint(
        name = "uq_patient_category_count_facility_id_report_id_patient_id_category_id",
        columnNames = {"facility_id", "report_id", "patient_id", "category_id"}))
public class PatientCategoryCount {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private String facilityId;

    @Column(nullable = false)
    private String reportId;

    @Column(nullable = false)
    private String patientId;

    @Column(nullable = false)
    private String categoryId;

    @Column(nullable = false)
    private long issues;
}
