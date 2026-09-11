using LantanaGroup.Link.DMRP.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.DMRP.Data.Repository.Mappings
{
    public class FacilityReportingPlanConfigMap : IEntityTypeConfiguration<FacilityReportingPlan>
    {
        /// <summary>
        /// Facility ids are NHSN Org Ids. Bounded so the column can take part in an index; an
        /// unbounded string maps to nvarchar(max), which SQL Server refuses to index.
        /// </summary>
        public const int FacilityIdMaxLength = 100;

        public const string UniquePeriodIndexName = "IX_FacilityReportingPlans_Facility_Mapping_Period";

        /// <summary>
        /// Matches the length EF Core gives a string primary key, so the foreign key column and
        /// MeasureMappings.Id are the same type.
        /// </summary>
        internal const int MeasureMappingIdMaxLength = 450;

        /// <summary>
        /// Long enough for the component codes DMRP publishes (MSC, PS) with room to spare, short
        /// enough to stay indexable.
        /// </summary>
        public const int ComponentMaxLength = 20;

        /// <summary>
        /// Matches the length the measure mappings themselves allow, so a measure name that can be
        /// mapped can also be recorded against an enrollment.
        /// </summary>
        public const int MeasureMaxLength = 255;

        public void Configure(EntityTypeBuilder<FacilityReportingPlan> builder)
        {
            builder.ToTable("FacilityReportingPlans");

            builder.HasKey(p => p.Id);

            builder.Property(p => p.FacilityId)
                .IsRequired()
                .HasMaxLength(FacilityIdMaxLength);

            // Optional: an enrollment is recorded whether or not Link has a mapping for its
            // measure yet.
            builder.Property(p => p.MeasureMappingId)
                .IsRequired(false)
                .HasMaxLength(MeasureMappingIdMaxLength);

            builder.Property(p => p.Measure)
                .IsRequired()
                .HasMaxLength(MeasureMaxLength);

            builder.Property(p => p.Component)
                .IsRequired()
                .HasMaxLength(ComponentMaxLength);

            builder.Property(p => p.ReportingMonth).IsRequired();
            builder.Property(p => p.ReportingYear).IsRequired();
            builder.Property(p => p.IsReporting).IsRequired();

            // Restrict rather than cascade: a mapping that reporting plans point at must not be
            // deletable out from under them, since the plan rows are the record of what DMRP said.
            builder.HasOne(p => p.MeasureMapping)
                .WithMany()
                .HasForeignKey(p => p.MeasureMappingId)
                .OnDelete(DeleteBehavior.Restrict);

            // One enrollment per facility, component, measure and period - enforced here so
            // concurrent writers cannot both pass the manager's pre-check and insert a duplicate.
            //
            // Keyed on the measure rather than on the mapping. The mapping is optional, and a
            // nullable column in the key would both leave unmapped rows unconstrained and change
            // what "duplicate" means the moment an admin mapped one. The measure is what DMRP
            // actually returns, and it is what stays constant.
            builder.HasIndex(p => new { p.FacilityId, p.Component, p.Measure, p.ReportingYear, p.ReportingMonth })
                .IsUnique()
                .HasDatabaseName(UniquePeriodIndexName);

            // Serves the by-facility and by-period reads.
            builder.HasIndex(p => new { p.FacilityId, p.ReportingYear, p.ReportingMonth })
                .HasDatabaseName("IX_FacilityReportingPlans_Facility_Period");
        }
    }
}
