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
        internal const int FacilityIdMaxLength = 100;

        /// <summary>
        /// Matches the length EF Core gives a string primary key, so the foreign key column and
        /// MeasureMappings.Id are the same type.
        /// </summary>
        internal const int MeasureMappingIdMaxLength = 450;

        public void Configure(EntityTypeBuilder<FacilityReportingPlan> builder)
        {
            builder.ToTable("FacilityReportingPlans");

            builder.HasKey(p => p.Id);

            builder.Property(p => p.FacilityId)
                .IsRequired()
                .HasMaxLength(FacilityIdMaxLength);

            builder.Property(p => p.MeasureMappingId)
                .IsRequired()
                .HasMaxLength(MeasureMappingIdMaxLength);

            builder.Property(p => p.ReportingMonth).IsRequired();
            builder.Property(p => p.ReportingYear).IsRequired();
            builder.Property(p => p.IsReporting).IsRequired();

            // Restrict rather than cascade: a mapping that reporting plans point at must not be
            // deletable out from under them, since the plan rows are the record of what DMRP said.
            builder.HasOne(p => p.MeasureMapping)
                .WithMany()
                .HasForeignKey(p => p.MeasureMappingId)
                .OnDelete(DeleteBehavior.Restrict);

            // "Each entry should have a unique combination of FacilityId, MeasureMappingId,
            // ReportMonth and ReportYear" - enforced here so concurrent writers cannot both pass the
            // manager's pre-check and insert a duplicate.
            builder.HasIndex(p => new { p.FacilityId, p.MeasureMappingId, p.ReportingMonth, p.ReportingYear })
                .IsUnique()
                .HasDatabaseName("IX_FacilityReportingPlans_Facility_Mapping_Period");

            // Serves the by-facility and by-period reads.
            builder.HasIndex(p => new { p.FacilityId, p.ReportingYear, p.ReportingMonth })
                .HasDatabaseName("IX_FacilityReportingPlans_Facility_Period");
        }
    }
}
