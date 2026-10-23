using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence.Configurations;

public class MrnIntakeOptionSetConfiguration : IEntityTypeConfiguration<MrnIntakeOptionSet>
{
    public void Configure(EntityTypeBuilder<MrnIntakeOptionSet> builder)
    {
        builder.ToTable("MrnIntakeOptionSets");
        builder.HasKey(x => x.Id);

        // One row per (group, value) — what GetOptionsAsync groups and orders by.
        builder.HasIndex(x => new { x.OptionGroup, x.Value }).IsUnique();

        builder.Property(x => x.OptionGroup).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(64).IsRequired();
        builder.Property(x => x.LabelKey).HasMaxLength(256).IsRequired();
    }
}
