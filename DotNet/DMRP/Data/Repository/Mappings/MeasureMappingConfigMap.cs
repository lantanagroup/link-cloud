using LantanaGroup.Link.DMRP.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.DMRP.Data.Repository.Mappings
{
    public class MeasureMappingConfigMap : IEntityTypeConfiguration<MeasureMapping>
    {
        public void Configure(EntityTypeBuilder<MeasureMapping> builder)
        {
            builder.ToTable("MeasureMappings");

            builder.HasKey(m => m.Id);

            builder.Property(m => m.Measure)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(m => m.DQM)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(m => m.Frequency)
                .IsRequired();

            builder.HasIndex(m => new { m.Measure, m.DQM })
                .IsUnique();
        }
    }
}
