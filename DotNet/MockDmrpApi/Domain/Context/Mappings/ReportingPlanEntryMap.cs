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
        builder.Property(e => e.Measure).IsRequired().HasMaxLength(50);
        builder.Property(e => e.IsReporting).IsRequired().HasMaxLength(10);
        builder.Property(e => e.ReportingMonth).IsRequired();
        builder.Property(e => e.ReportingYear).IsRequired();
        builder.Property(e => e.CreateDate).IsRequired();

        // One composite index does three jobs:
        //   * GET /facilities/{facilityId}  -- seek on the leading column
        //   * GET /reporting-plans          -- seek on the first three columns
        //   * natural-key uniqueness
        //
        // Column order is (FacilityId, Year, Month, Measure) rather than the
        // order the fields read in, so that both query patterns are seeks.
        //
        // Uniqueness is what stops the table holding two contradictory rows for
        // the same facility, measure and period, which would otherwise produce a
        // nonsensical reporting plan.
        builder.HasIndex(e => new { e.FacilityId, e.ReportingYear, e.ReportingMonth, e.Measure })
               .IsUnique()
               .HasDatabaseName("UX_MockDmrpEntries_Facility_Period_Measure");
    }
}
