using Census.Domain.Entities;
using LantanaGroup.Link.Census.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Census.Domain.Context;

public class CensusContext : DbContext
{
    public DbSet<CensusConfigEntity> CensusConfigs { get; set; }
    public DbSet<CensusPatientListEntity> CensusPatientLists { get; set; }
    public DbSet<PatientCensusHistoricEntity> PatientCensusHistorics { get; set; }

    public CensusContext(DbContextOptions<CensusContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CensusConfigEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<CensusPatientListEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<PatientCensusHistoricEntity>().HasKey(x => x.Id);
    }
}
