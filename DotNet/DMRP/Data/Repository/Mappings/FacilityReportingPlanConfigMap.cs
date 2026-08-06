using LantanaGroup.Link.DMRP.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.DMRP.Data.Repository.Mappings
{
    public class FacilityReportingPlanConfigMap : IEntityTypeConfiguration<FacilityReportingPlan>
    {
        public void Configure(EntityTypeBuilder<FacilityReportingPlan> builder)
        {
            builder.ToTable("FacilityReportingPlans");

            builder.HasKey(p => p.Id);
        }
    }
}
