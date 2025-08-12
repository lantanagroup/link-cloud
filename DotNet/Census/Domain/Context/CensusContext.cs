using Census.Domain.Entities;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace LantanaGroup.Link.Census.Domain.Context;

public class CensusContext : DbContext
{
    public DbSet<CensusConfigEntity> CensusConfigs { get; set; }
    public DbSet<RetryEntity> RetryEntities { get; set; }
    public DbSet<PatientEvent> PatientEvents { get; set; }
    public DbSet<PatientEncounter> PatientEncounters { get; set; }
    public DbSet<PatientVisitIdentifier> PatientVisitIdentifiers { get; set; }
    public DbSet<PatientIdentifier> PatientIdentifiers { get; set; }

    public CensusContext(DbContextOptions<CensusContext> options) : base(options)
    {
    }

    public CensusContext() : base() { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CensusConfigEntity>()
            .Property(b => b.Id)
            .HasConversion(
                v => new Guid(v),
                v => v.ToString()
            );

        modelBuilder.Entity<RetryEntity>()
            .Property(x => x.Headers)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, new JsonSerializerOptions())
        );

        modelBuilder.Entity<PatientEncounter>()
                .HasMany(x => x.PatientVisitIdentifiers)
                .WithOne(x => x.PatientEncounter)
                .HasForeignKey(x => x.PatientEncounterId).IsRequired();

        modelBuilder.Entity<PatientEncounter>()
            .HasMany(x => x.PatientIdentifiers)
            .WithOne(x => x.PatientEncounter)
            .HasForeignKey(x => x.PatientEncounterId).IsRequired();
    }

    //IMPORTANT!!!!!!!!!
    //uncomment this section if you want to use the design-time factory for migrations
    //otherwise dotnet ef migrations will not work properly
    //public class CensusContextFactory : IDesignTimeDbContextFactory<CensusContext>
    //{
    //    public CensusContext CreateDbContext(string[] args)
    //    {
    //        var optionsBuilder = new DbContextOptionsBuilder<CensusContext>();
    //        optionsBuilder.UseSqlServer();

    //        return new CensusContext(optionsBuilder.Options);
    //    }
    //}
}