using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence.Configurations;

public class NhsnUserConfiguration : IEntityTypeConfiguration<NhsnUser>
{
    public void Configure(EntityTypeBuilder<NhsnUser> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.ExternalUserId).IsUnique();
        builder.HasIndex(x => x.Email);
        builder.Property(x => x.ExternalUserId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.GroupsRaw).HasMaxLength(256);
        builder.Property(x => x.FacilityId).HasMaxLength(64);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.LastModifiedBy).HasMaxLength(256);
    }
}