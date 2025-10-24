using AppAny.Quartz.EntityFrameworkCore.Migrations;
using AppAny.Quartz.EntityFrameworkCore.Migrations.SqlServer;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace QueryDispatch.Domain.Context;

public class QueryDispatchDbContext : DbContext
{


    public QueryDispatchDbContext(DbContextOptions<QueryDispatchDbContext> options) : base(options)
    {
    }

    //public QueryDispatchDbContext() : base()
    //{
    //}

    public DbSet<QueryDispatchConfigurationEntity> QueryDispatchConfigurations { get; set; }
    public DbSet<ScheduledReportEntity> ScheduledReports { get; set; }
    public DbSet<PatientDispatchEntity> PatientDispatches { get; set; }
    public DbSet<RetryEntity> EventRetries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScheduledReportEntity>()
            .Property(b => b.ReportPeriods)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<List<ReportPeriodEntity>>(v, new JsonSerializerOptions())
            );

        modelBuilder.Entity<PatientDispatchEntity>()
            .Property(b => b.ScheduledReportPeriods)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<List<ReportPeriodEntity>>(v, new JsonSerializerOptions())
            );

        modelBuilder.Entity<QueryDispatchConfigurationEntity>()
            .Property(b => b.DispatchSchedules)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<List<DispatchSchedule>>(v, new JsonSerializerOptions())
            );

        modelBuilder.Entity<RetryEntity>()
            .Property(b => b.Headers)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, new JsonSerializerOptions())
            );

        // Adds Quartz.NET SqlServer schema to EntityFrameworkCore
        modelBuilder.AddQuartz(builder => builder.UseSqlServer());
    }

    //--------------------IMPORTANT--------------------
    //Uncomment the class QueryDispatchDbContextFactory to create or apply migrations. Ensure that you enter the correct connection string in the UseSqlServer method.
    //When finished, delete the connection string and comment out the class QueryDispatchDbContextFactory.
    //commands:
    //dotnet ef migrations add <NAME OF MIGRATION>
    //dotnet ef database update

    //public class QueryDispatchDbContextFactory : IDesignTimeDbContextFactory<QueryDispatchDbContext>
    //{
    //    public QueryDispatchDbContext CreateDbContext(string[] args)
    //    {
    //        var optionsBuilder = new DbContextOptionsBuilder<QueryDispatchDbContext>();
    //        optionsBuilder.UseSqlServer("");

    //        return new QueryDispatchDbContext(optionsBuilder.Options);
    //    }
    //}
}
