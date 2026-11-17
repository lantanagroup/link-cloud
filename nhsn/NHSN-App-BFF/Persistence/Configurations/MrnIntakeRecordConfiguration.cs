using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence.Configurations;

public class MrnIntakeRecordConfiguration : IEntityTypeConfiguration<MrnIntakeRecord>
{
    public void Configure(EntityTypeBuilder<MrnIntakeRecord> builder)
    {
        builder.ToTable("MrnIntakeRecords");
        builder.HasKey(x => x.Id);

        // One MRN intake record per facility - the unique index is what makes get-or-create safe
        // under concurrency, same reasoning as OnboardingDraftConfiguration.
        builder.HasIndex(x => x.FacilityId).IsUnique();

        builder.Property(x => x.FacilityId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.IntakeJson).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }
}
