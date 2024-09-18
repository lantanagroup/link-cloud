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

            /*  var comp = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                  (c1, c2) => c1.SequenceEqual(c2),
                  c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                  c => c.ToList());*/

            builder.OwnsOne(facilityConfig => facilityConfig.ScheduledReports, navBuilder =>
            {
                navBuilder.ToJson();

            });

            /* navBuilder.OwnsMany(st => st.ReportTypeSchedules, navBuilder =>
             {
                 navBuilder.ToJson();
                 navBuilder.Property(st => st.ScheduledTriggers).HasConversion(v => JsonConvert.SerializeObject(v), v => JsonConvert.DeserializeObject<List<string>>(v)).Metadata.SetValueComparer(comp);
             });*/



            /* builder.OwnsMany(facilityConfig => facilityConfig.MonthlyReportingPlans, navBuilder =>
             {
                 navBuilder.ToJson();
             });*/
        }
    }
}
