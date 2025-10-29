using AppAny.Quartz.EntityFrameworkCore.Migrations;
using AppAny.Quartz.EntityFrameworkCore.Migrations.SqlServer;
using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LantanaGroup.Link.Census.Domain.Context;

public class CensusContext : DbContext
{
    public DbSet<CensusConfigEntity> CensusConfigs { get; set; }
    public DbSet<PatientEvent> PatientEvents { get; set; }
    public DbSet<PatientEncounter> PatientEncounters { get; set; }
    public DbSet<PatientVisitIdentifier> PatientVisitIdentifiers { get; set; }
    public DbSet<PatientIdentifier> PatientIdentifiers { get; set; }

    public CensusContext(DbContextOptions<CensusContext> options) : base(options)
    {
    }

    public CensusContext() : base()
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CensusConfigEntity>()
            .Property(b => b.Id)
            .HasConversion(
                v => new Guid(v),
                v => v.ToString()
            );


        modelBuilder.Entity<PatientEncounter>()
            .HasMany(x => x.PatientVisitIdentifiers)
            .WithOne(x => x.PatientEncounter)
            .HasForeignKey(x => x.PatientEncounterId).IsRequired();

        // Configure the PayloadJsonConverter
        var payloadConverter = new PayloadJsonConverter();

        modelBuilder.Entity<PatientEvent>()
            .Property(e => e.Payload)
            .HasConversion(
                // Serialize
                v => JsonSerializer.Serialize(v, typeof(IPayload), JsonSerializerOptionsProvider.Options),
                // Deserialize
                v => JsonSerializer.Deserialize<IPayload>(v, JsonSerializerOptionsProvider.Options)
            );

        // Add indexes for PatientEvent
        modelBuilder.Entity<PatientEvent>()
            .HasIndex(e => e.FacilityId)
            .HasDatabaseName("IX_PatientEvents_FacilityId");

        modelBuilder.Entity<PatientEvent>()
            .HasIndex(e => e.CorrelationId)
            .HasDatabaseName("IX_PatientEvents_CorrelationId");

        modelBuilder.Entity<PatientEvent>()
            .HasIndex(e => e.SourcePatientId)
            .HasDatabaseName("IX_PatientEvents_SourcePatientId");

        modelBuilder.Entity<PatientEvent>()
            .HasIndex(e => e.CreateDate)
            .HasDatabaseName("IX_PatientEvents_CreateDate");

        // Add composite indexes for better query performance
        modelBuilder.Entity<PatientEvent>()
            .HasIndex(e => new { e.CorrelationId, e.CreateDate })
            .HasDatabaseName("IX_PatientEvents_CorrelationId_CreateDate");

        // Add indexes for PatientEncounter
        modelBuilder.Entity<PatientEncounter>()
            .HasIndex(e => e.Id)
            .HasDatabaseName("IX_PatientEncounters_Id");

        modelBuilder.Entity<PatientEncounter>()
            .HasIndex(e => e.CorrelationId)
            .HasDatabaseName("IX_PatientEncounters_CorrelationId");

        modelBuilder.Entity<PatientEncounter>()
            .HasIndex(e => e.FacilityId)
            .HasDatabaseName("IX_PatientEncounters_FacilityId");

        modelBuilder.Entity<PatientEncounter>()
            .HasIndex(e => e.AdmitDate)
            .HasDatabaseName("IX_PatientEncounters_AdmitDate");

        modelBuilder.Entity<PatientEncounter>()
            .HasIndex(e => e.DischargeDate)
            .HasDatabaseName("IX_PatientEncounters_DischargeDate");

        // Add composite indexes for common query patterns
        modelBuilder.Entity<PatientEncounter>()
            .HasIndex(e => new { e.FacilityId, e.AdmitDate })
            .HasDatabaseName("IX_PatientEncounters_FacilityId_AdmitDate");

        modelBuilder.Entity<PatientEncounter>()
            .HasIndex(e => new { e.FacilityId, e.DischargeDate })
            .HasDatabaseName("IX_PatientEncounters_FacilityId_DischargeDate");

        // Adds Quartz.NET SqlServer schema to EntityFrameworkCore
        modelBuilder.AddQuartz(builder => builder.UseSqlServer());
    }

    public static class JsonSerializerOptionsProvider
    {
        public static JsonSerializerOptions Options { get; } = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        static JsonSerializerOptionsProvider()
        {
            Options.Converters.Add(new PayloadJsonConverter());
        }
    }


    //IMPORTANT!!!!!!!!!
    //uncomment this section if you want to use the design-time factory for migrations
    //otherwise dotnet ef migrations will not work properly
    // public class CensusContextFactory : IDesignTimeDbContextFactory<CensusContext>
    // {
    //     public CensusContext CreateDbContext(string[] args)
    //     {
    //         var optionsBuilder = new DbContextOptionsBuilder<CensusContext>();
    //         optionsBuilder.UseSqlServer();
    //
    //         // // Tell EF Core to skip DB connection validation at design time
    //         // optionsBuilder.ConfigureWarnings(w => 
    //         //     w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ContextInitialized));
    //
    //         
    //         return new CensusContext(optionsBuilder.Options);
    //     }
    // }
}