package com.lantanagroup.link.validation.entities;

import jakarta.persistence.*;
import lombok.Getter;
import lombok.Setter;

import java.time.OffsetDateTime;

/**
 * Marks that a report's category counts have been written. Without it, a report with no category rows could mean
 * either "no findings" or "counts never written". Its row is also locked while a patient's counts are applied, which
 * serializes the updates to one report's totals.
 */
@Getter
@Setter
@Entity
@Table(uniqueConstraints = @UniqueConstraint(
        name = "uq_report_category_count_status_facility_id_report_id",
        columnNames = {"facility_id", "report_id"}))
public class ReportCategoryCountStatus {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private String facilityId;

    @Column(nullable = false)
    private String reportId;

    @Column(nullable = false)
    private int patientsCounted;

    @Column(nullable = false)
    private OffsetDateTime lastUpdated;
}
