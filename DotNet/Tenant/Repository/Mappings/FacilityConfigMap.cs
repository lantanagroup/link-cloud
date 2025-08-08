using LantanaGroup.Link.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.Tenant.Repository.Mapping
{
    public class FacilityConfigMap : IEntityTypeConfiguration<Facility>
    {
        public void Configure(EntityTypeBuilder<Facility> builder)
        {
            builder.ToTable("Facilities");

            builder.HasKey(b => b.Id).IsClustered(false);

            builder.OwnsOne(facilityConfig => facilityConfig.ScheduledReports, navBuilder =>
            {
                navBuilder.ToJson();

            });
        }
    }
}
