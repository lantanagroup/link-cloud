package com.lantanagroup.link.validation.entities;

import jakarta.persistence.*;
import lombok.Getter;
import lombok.Setter;
import org.hibernate.annotations.Nationalized;

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

    // nvarchar, like the driver's string parameters, so SQL Server seeks instead of scanning.
    // See dev-docs/validation-category-counts.md.
    @Nationalized
    @Column(nullable = false)
    private String facilityId;

    @Nationalized
    @Column(nullable = false, length = 64)
    private String reportId;

    @Column(nullable = false)
    private OffsetDateTime lastUpdated;
}
