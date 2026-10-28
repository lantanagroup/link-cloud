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

            // Nullable: a measure the sync recorded but nobody has mapped yet has no dQM. The unique
            // index below is on the measure alone, so that measure holds exactly one row whether or
            // not a dQM has been supplied for it, and completing it fills this column in place.
            builder.Property(m => m.DQM)
                .HasMaxLength(255);

            builder.Property(m => m.Frequency)
                .IsRequired();

            // One row per NHSN measure, not per measure and dQM. A measure maps to exactly one dQM,
            // so a second row for the same measure is a mistake rather than a refinement; several
            // measures sharing one dQM is normal and stays allowed. Keying on the measure alone also
            // means the row the sync records with no dQM is the same row an administrator completes,
            // rather than something a second row can quietly supersede.
            builder.HasIndex(m => m.Measure)
                .IsUnique();
        }
    }
}
