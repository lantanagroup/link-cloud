using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.MockDmrpApi.Domain.Context.Mappings;

public class ReportingPlanEntryMap : IEntityTypeConfiguration<ReportingPlanEntryEntity>
{
    public void Configure(EntityTypeBuilder<ReportingPlanEntryEntity> builder)
    {
        builder.ToTable("MockDmrpEntries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(50).ValueGeneratedNever();

        builder.Property(e => e.FacilityId).IsRequired().HasMaxLength(120);
        builder.Property(e => e.Component).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Measure).IsRequired().HasMaxLength(50);
        builder.Property(e => e.IsReporting).IsRequired().HasMaxLength(10);
        builder.Property(e => e.ReportingYear).IsRequired();
        builder.Property(e => e.CreateDate).IsRequired();

        // ReportingMonth is deliberately nullable: MSC is reported monthly and carries one,
        // PS is annual and does not. Whether it must be present is a rule about the
        // component, which a column cannot express, so the service enforces it.
        builder.Property(e => e.ReportingMonth);

        // One composite index does three jobs:
        //   * GET /api/mock-dmrp/facilities/{facilityId}/entries  -- seek on the leading column
        //   * GET /msc and GET /ps/annual                         -- seek on the leading columns
        //   * natural-key uniqueness
        //
        // Column order is (FacilityId, Component, Year, Month, Measure) rather than the
        // order the fields read in, so that every query pattern is a seek. Component sits
        // second because both plan queries filter on it.
        //
        // Uniqueness is what stops the table holding two contradictory rows for the same
        // facility, component, measure and period, which would otherwise produce a
        // nonsensical reporting plan.
        //
        // SQL Server treats NULL as a value here and permits only one per key combination,
        // which is exactly what is wanted: one PS row per (facility, year, measure), one MSC
        // row per (facility, year, month, measure).
        //
        // HasFilter(null) is load-bearing. For a unique index over a nullable column EF
        // defaults to a filtered index -- "WHERE [ReportingMonth] IS NOT NULL" -- which would
        // drop every annual row out of the index and silently allow duplicate PS entries,
        // the exact thing this index exists to prevent.
        builder.HasIndex(e => new { e.FacilityId, e.Component, e.ReportingYear, e.ReportingMonth, e.Measure })
               .IsUnique()
               .HasFilter(null)
               .HasDatabaseName("UX_MockDmrpEntries_Facility_Component_Period_Measure");
    }
}
