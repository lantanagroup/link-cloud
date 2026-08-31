using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence.Configurations;

public class NhsnFacilityConfiguration : IEntityTypeConfiguration<NhsnFacility>
{
    public void Configure(EntityTypeBuilder<NhsnFacility> builder)
    {
        builder.ToTable("Facilities");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.FacilityId).IsUnique();
        builder.Property(x => x.FacilityId).HasMaxLength(64).IsRequired();

        // Stored as their names rather than ordinals: a reordered or inserted enum member would
        // silently reinterpret every existing row if these were ints.
        builder.Property(x => x.OnboardingStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Vendor).HasConversion<string>().HasMaxLength(32);

        builder.Property(x => x.CurrentStepId).HasMaxLength(64);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.LastModifiedBy).HasMaxLength(256);

        // IsOnboarded is [NotMapped] and derived from OnboardingStatus. Do not add a column for it
        // — the pre-existing one is dropped by the migration that adds these.
        builder.Ignore(x => x.IsOnboarded);
    }
}
