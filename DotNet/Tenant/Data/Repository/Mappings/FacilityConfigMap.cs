using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.Tenant.Data.Repository.Mappings
{
    public class FacilityConfigMap : IEntityTypeConfiguration<Facility>
    {
        public void Configure(EntityTypeBuilder<Facility> builder)
        {
            builder.ToTable("Facilities");

            builder.HasKey(b => b.Id).IsClustered(false);

            // Map an empty/NULL Vendor column to null instead of the default enum value (Epic).
            builder.Property(f => f.Vendor)
                .HasMaxLength(50)
                .HasConversion(
                    v => v.HasValue ? v.Value.ToString() : null,
                    v => string.IsNullOrEmpty(v) ? (Vendor?)null : Enum.Parse<Vendor>(v));

            builder.OwnsOne(facilityConfig => facilityConfig.ScheduledReports, navBuilder =>
            {
                navBuilder.ToJson();

            });
        }
    }
}
