using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Data.Repository.Mappings;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.DMRP.Data.Repository;

public class DmrpDbContext : DbContext
{
    public DmrpDbContext(DbContextOptions<DmrpDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<MeasureMapping> MeasureMappings { get; set; } = null!;

    public virtual DbSet<FacilityReportingPlan> FacilityReportingPlans { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new MeasureMappingConfigMap());
        modelBuilder.ApplyConfiguration(new FacilityReportingPlanConfigMap());
    }
}
