using AppAny.Quartz.EntityFrameworkCore.Migrations;
using AppAny.Quartz.EntityFrameworkCore.Migrations.SqlServer;
using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace LantanaGroup.Link.Census.Domain.Context;

public class CensusContext : DbContext
{
    public DbSet<CensusConfig> CensusConfigs { get; set; }
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

        modelBuilder.Entity<CensusConfig>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Enabled).HasDefaultValue(true);
        });

        modelBuilder.Entity<PatientEncounter>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<PatientEvent>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.EventType).HasConversion(new EnumToStringConverter<EventType>());
            entity.Property(e => e.SourceType).HasConversion(new EnumToStringConverter<SourceType>());

            entity.Property(e => e.Payload).HasConversion(
                // Serialize
                v => JsonSerializer.Serialize(v, typeof(IPayload), JsonSerializerOptionsProvider.Options),
                // Deserialize
                v => JsonSerializer.Deserialize<IPayload>(v, JsonSerializerOptionsProvider.Options));
        });

        modelBuilder.Entity<PatientIdentifier>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd().HasDefaultValueSql("(newid())");
            entity.Property(e => e.SourceType).HasConversion(new EnumToStringConverter<SourceType>());
        });

        modelBuilder.Entity<PatientVisitIdentifier>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd().HasDefaultValueSql("(newid())");
            entity.Property(e => e.SourceType).HasConversion(new EnumToStringConverter<SourceType>());
        });

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
}