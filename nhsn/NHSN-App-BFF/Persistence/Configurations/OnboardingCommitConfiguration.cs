using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence.Configurations;

public class OnboardingCommitConfiguration : IEntityTypeConfiguration<OnboardingCommit>
{
    public void Configure(EntityTypeBuilder<OnboardingCommit> builder)
    {
        builder.ToTable("OnboardingCommits");
        builder.HasKey(x => x.Id);

        // One commit result per facility - the unique index is what makes get-or-replace safe
        // under concurrency, same reasoning as OnboardingDraftConfiguration.
        builder.HasIndex(x => x.FacilityId).IsUnique();

        builder.Property(x => x.FacilityId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResultJson).IsRequired();
    }
}
