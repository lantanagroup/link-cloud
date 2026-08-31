using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence.Configurations;

public class OnboardingDraftConfiguration : IEntityTypeConfiguration<OnboardingDraft>
{
    public void Configure(EntityTypeBuilder<OnboardingDraft> builder)
    {
        builder.ToTable("OnboardingDrafts");
        builder.HasKey(x => x.Id);

        // One draft per facility. The unique index is what makes "get or create" safe under
        // concurrency: a duplicate insert fails on the index rather than silently producing a
        // second draft that half the requests would read.
        builder.HasIndex(x => x.FacilityId).IsUnique();

        builder.Property(x => x.FacilityId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DraftJson).IsRequired();
        builder.Property(x => x.UnlockedStepsJson).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }
}
