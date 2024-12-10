using LantanaGroup.Link.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;

namespace LantanaGroup.Link.Tenant.Repository.Mapping
{
    public class FacilityConfigMap : IEntityTypeConfiguration<FacilityConfigModel>
    {
        public void Configure(EntityTypeBuilder<FacilityConfigModel> builder)
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
