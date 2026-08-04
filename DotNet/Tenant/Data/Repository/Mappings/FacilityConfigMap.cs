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

            builder.HasOne(f => f.VendorVersion)
                .WithMany()
                .HasForeignKey(f => f.VendorVersionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.OwnsOne(facilityConfig => facilityConfig.ScheduledReports, navBuilder =>
            {
                navBuilder.ToJson();

            });
        }
    }
}
