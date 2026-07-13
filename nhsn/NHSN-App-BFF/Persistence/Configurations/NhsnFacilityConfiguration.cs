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
    }
}