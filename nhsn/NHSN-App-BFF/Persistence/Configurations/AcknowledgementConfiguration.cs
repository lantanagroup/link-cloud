using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence.Configurations;

public class AcknowledgementConfiguration : IEntityTypeConfiguration<Acknowledgement>
{
    public void Configure(EntityTypeBuilder<Acknowledgement> builder)
    {
        builder.ToTable("Acknowledgements");
        builder.HasKey(x => x.Id);

        // Not unique - append-only means more than one row can exist for the same facility and
        // Kind. This index is for finding the latest one quickly, not for uniqueness.
        builder.HasIndex(x => new { x.FacilityId, x.Kind, x.AcceptedOn });

        builder.Property(x => x.FacilityId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.ContextId).HasMaxLength(64);
        builder.Property(x => x.StatementKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.AcceptedByExternalUserId).HasMaxLength(256).IsRequired();
    }
}
