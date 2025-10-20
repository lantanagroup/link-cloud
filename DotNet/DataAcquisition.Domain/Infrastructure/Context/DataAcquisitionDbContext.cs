using AppAny.Quartz.EntityFrameworkCore.Migrations;
using AppAny.Quartz.EntityFrameworkCore.Migrations.SqlServer;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using ScheduledReport = LantanaGroup.Link.Shared.Application.Models.ScheduledReport;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;

public class DataAcquisitionDbContext : DbContext
{
    public DataAcquisitionDbContext(DbContextOptions<DataAcquisitionDbContext> options) : base(options)
    {
    }

    public DbSet<FhirQueryConfiguration> FhirQueryConfigurations { get; set; }
    public DbSet<FhirListConfiguration> FhirListConfigurations { get; set; }
    public DbSet<QueryPlan> QueryPlan { get; set; }
    public DbSet<ReferenceResources> ReferenceResources { get; set; }
    public DbSet<FhirQuery> FhirQueries { get; set; }
    public DbSet<RetryEntity> RetryEntities { get; set; }
    public DbSet<DataAcquisitionLog> DataAcquisitionLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //-------------------QueryPlan-------------------

        modelBuilder.Entity<QueryPlan>()
            .Property(b => b.Id)
            .HasConversion(
                v => new Guid(v),
                v => v.ToString()
            );

        modelBuilder.Entity<QueryPlan>()
        .Property(b => b.InitialQueries)
        .HasConversion(
            v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
            v => JsonSerializer.Deserialize<Dictionary<string, IQueryConfig>>(v, new JsonSerializerOptions())
            );

        modelBuilder.Entity<QueryPlan>()
        .Property(b => b.SupplementalQueries)
        .HasConversion(
            v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
            v => JsonSerializer.Deserialize<Dictionary<string, IQueryConfig>>(v, new JsonSerializerOptions())
            );

        //-------------------FhirQueryConfiguration-------------------

        modelBuilder.Entity<FhirQueryConfiguration>()
            .Property(b => b.Id)
            .HasConversion(
                v => new Guid(v),
                v => v.ToString()
            );

        modelBuilder.Entity<FhirQueryConfiguration>()
            .Property(b => b.Authentication)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<AuthenticationConfiguration>(v, new JsonSerializerOptions())
        );

        //-------------------FhirListConfiguration-------------------

        modelBuilder.Entity<FhirListConfiguration>()
            .Property(b => b.Id)
            .HasConversion(
                v => new Guid(v),
                v => v.ToString()
            );

        modelBuilder.Entity<FhirListConfiguration>()
            .Property(b => b.Authentication)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<AuthenticationConfiguration>(v, new JsonSerializerOptions())
            );

        modelBuilder.Entity<FhirListConfiguration>()
            .Property(p => p.EHRPatientLists)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<List<EhrPatientList>>(v, new JsonSerializerOptions())
        );

        //-------------------ReferenceResources-------------------
        modelBuilder.Entity<ReferenceResources>()
            .Property(b => b.Id)
            .HasConversion(
                v => new Guid(v),
                v => v.ToString()
            );

        modelBuilder.Entity<ReferenceResources>()
            .Property(b => b.QueryPhase)
            .HasConversion<string>();


        //-------------------Retry Repository//-------------------
        modelBuilder.Entity<RetryEntity>()
            .Property(x => x.Headers)
            .HasConversion(
                           v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                           v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, new JsonSerializerOptions()));

        //-------------------FhirQuery-------------------
        modelBuilder.Entity<FhirQuery>()
            .Property(b => b.Id)
            .HasConversion(
                v => new Guid(v),
                v => v.ToString()
            );

        modelBuilder.Entity<FhirQuery>()
            .Property(b => b.QueryType)
            .HasConversion<string>();

        modelBuilder.Entity<FhirQuery>()
            .HasMany(x => x.ResourceReferenceTypes)
            .WithOne(x => x.FhirQueryRef)
            .HasForeignKey(x => x.FhirQueryId)
            .HasPrincipalKey(x => x.Id);

        modelBuilder.Entity<FhirQuery>()
            .Property(d => d.ResourceTypes)
            .HasConversion(
                v => JsonSerializer.Serialize(v.Select(rt => rt.ToString()), new JsonSerializerOptions()), // Serialize as enum names
                v => JsonSerializer.Deserialize<List<string>>(v, new JsonSerializerOptions())
                    .Select(rt => Enum.Parse<Hl7.Fhir.Model.ResourceType>(rt)).ToList() // Deserialize back to enum
            );

        //-------------------DataAcquisitionLog-------------------
        modelBuilder.Entity<DataAcquisitionLog>()
            .Property(b => b.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<DataAcquisitionLog>()
            .HasMany(x => x.FhirQuery)
            .WithOne(x => x.DataAcquisitionLog)
            .HasForeignKey(x => x.DataAcquisitionLogId)
            .HasPrincipalKey(x => x.Id);

        modelBuilder.Entity<DataAcquisitionLog>()
            .Property(d => d.ScheduledReport)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<ScheduledReport>(v, new JsonSerializerOptions())
            );

        modelBuilder.Entity<DataAcquisitionLog>()
            .Property(d => d.Priority)
            .HasConversion<string>();

        modelBuilder.Entity<DataAcquisitionLog>()
            .Property(d => d.QueryPhase)
            .HasConversion<string>();

        modelBuilder.Entity<DataAcquisitionLog>()
            .Property(d => d.Status)
            .HasConversion<string>();

        modelBuilder.Entity<DataAcquisitionLog>()
            .Property(d => d.QueryType)
            .HasConversion<string>();

        //-------------------ResourceReferenceType-------------------
        modelBuilder.Entity<ResourceReferenceType>()
            .Property(b => b.QueryPhase)
            .HasConversion<string>();


        // Prefix and schema can be passed as parameters
        // Adds Quartz.NET SqlServer schema to EntityFrameworkCore
        modelBuilder.AddQuartz(builder => builder.UseSqlServer());

    }

    public class DataAcquisitionDbContextFactory : IDesignTimeDbContextFactory<DataAcquisitionDbContext>
    {
        public DataAcquisitionDbContext CreateDbContext(string[] args)
        {
            string env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).FullName, "DataAcquisition"))
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env}.json", optional: true)
                //.AddEnvironmentVariables()
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<DataAcquisitionDbContext>();
            var connectionString = config.GetConnectionString("SqlServer");
            optionsBuilder.UseSqlServer(connectionString);

            return new DataAcquisitionDbContext(optionsBuilder.Options);
        }
    }
}
