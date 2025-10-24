using AppAny.Quartz.EntityFrameworkCore.Migrations;
using AppAny.Quartz.EntityFrameworkCore.Migrations.SqlServer;
using Census.Domain.Entities;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace LantanaGroup.Link.Census.Domain.Context;

public class CensusContext : DbContext
{
    public DbSet<CensusConfigEntity> CensusConfigs { get; set; }
    public DbSet<CensusPatientListEntity> CensusPatientLists { get; set; }
    public DbSet<PatientCensusHistoricEntity> PatientCensusHistorics { get; set; }
    public DbSet<RetryEntity> RetryEntities { get; set; }

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
        modelBuilder.Entity<PatientCensusHistoricEntity>()
            .Property(b => b.Id)
            .HasConversion(
                v => new Guid(v),
                v => v.ToString()
            );
        modelBuilder.Entity<CensusPatientListEntity>()
            .Property(b => b.Id)
            .HasConversion(
                v => new Guid(v),
                v => v.ToString()
            );

        modelBuilder.Entity<PatientCensusHistoricEntity>()
            .Property(x => x.ReportId)
            .HasComputedColumnSql("CONCAT(FacilityId, '-', CensusDateTime)");

        modelBuilder.Entity<RetryEntity>()
            .Property(x => x.Headers)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, new JsonSerializerOptions())
        );

        // Adds Quartz.NET SqlServer schema to EntityFrameworkCore
        modelBuilder.AddQuartz(builder => builder.UseSqlServer());
    }

    public class CensusContextFactory : IDesignTimeDbContextFactory<CensusContext>
    {
        public CensusContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<CensusContext>();
            optionsBuilder.UseSqlServer();

            return new CensusContext(optionsBuilder.Options);
        }
    }
}