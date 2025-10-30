using AppAny.Quartz.EntityFrameworkCore.Migrations;
using AppAny.Quartz.EntityFrameworkCore.Migrations.SqlServer;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
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
    public virtual DbSet<FhirQueryResourceType> FhirQueryResourceTypes { get; set; }
    public DbSet<DataAcquisitionLog> DataAcquisitionLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //-------------------QueryPlan-------------------

        modelBuilder.Entity<QueryPlan>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(b => b.InitialQueries)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => JsonSerializer.Deserialize<Dictionary<string, IQueryConfig>>(v, new JsonSerializerOptions()));

            entity.Property(b => b.SupplementalQueries)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                        v => JsonSerializer.Deserialize<Dictionary<string, IQueryConfig>>(v, new JsonSerializerOptions()));
        });

        //-------------------FhirQueryConfiguration-------------------

        modelBuilder.Entity<FhirQueryConfiguration>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(b => b.Authentication)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<AuthenticationConfiguration>(v, new JsonSerializerOptions()));
        });

        //-------------------FhirListConfiguration-------------------

        modelBuilder.Entity<FhirListConfiguration>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(b => b.Authentication)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<AuthenticationConfiguration>(v, new JsonSerializerOptions()));

            entity.Property(p => p.EHRPatientLists)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }),
                v => JsonSerializer.Deserialize<List<EhrPatientList>>(v, new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));
        });

        //-------------------ReferenceResources-------------------
        modelBuilder.Entity<ReferenceResources>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.QueryPhase).HasConversion(new EnumToStringConverter<QueryPhase>());
            entity.HasOne(d => d.DataAcquisitionLog).WithMany(p => p.ReferenceResources).HasConstraintName("FK_ReferenceResources_DataAcquisitionLog");
        });

        //-------------------FhirQuery-------------------

        modelBuilder.Entity<FhirQuery>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(b => b.QueryType)
            .HasConversion(new EnumToStringConverter<FhirQueryType>());

            entity.HasOne(d => d.DataAcquisitionLog).WithMany(p => p.FhirQueries)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FhirQuery_DataAcquisitionLog");

            entity.HasMany(d => d.FhirQueryResourceTypes).WithOne(p => p.FhirQuery)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasForeignKey(r => r.FhirQueryId)
                    .HasPrincipalKey(q => q.Id);
        });

        modelBuilder.Entity<FhirQueryResourceType>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.ResourceType).HasConversion(new EnumToStringConverter<Hl7.Fhir.Model.ResourceType>());

            entity.HasOne(d => d.FhirQuery).WithMany(p => p.FhirQueryResourceTypes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FhirQueryResourceType_FhirQuery");
        });

        //-------------------DataAcquisitionLog-------------------
        modelBuilder.Entity<DataAcquisitionLog>(entity =>
        {
            entity.Property(b => b.Id).ValueGeneratedOnAdd();

            entity.HasMany(x => x.FhirQueries)
            .WithOne(x => x.DataAcquisitionLog)
            .HasForeignKey(x => x.DataAcquisitionLogId)
            .HasPrincipalKey(x => x.Id);

            entity.Property(d => d.ScheduledReport)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<ScheduledReport>(v, new JsonSerializerOptions())
            );

            entity.Property(d => d.Priority)
                .HasConversion(new EnumToStringConverter<AcquisitionPriority>());

            entity.Property(d => d.QueryPhase)
                .HasConversion(new EnumToStringConverter<QueryPhase>());

            entity.Property(d => d.Status)
                .HasConversion(new EnumToStringConverter<RequestStatus>());

            entity.Property(d => d.QueryType)
                .HasConversion(new EnumToStringConverter<FhirQueryType>());
        });

        //-------------------ResourceReferenceType-------------------
        modelBuilder.Entity<ResourceReferenceType>()
            .Property(b => b.Id).ValueGeneratedOnAdd();

        modelBuilder.Entity<ResourceReferenceType>()
            .Property(b => b.QueryPhase)
            .HasConversion(new EnumToStringConverter<QueryPhase>());

        // Prefix and schema can be passed as parameters
        // Adds Quartz.NET SqlServer schema to EntityFrameworkCore
        modelBuilder.AddQuartz(builder => builder.UseSqlServer());


        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Find the 'Id' property if it's a Guid
            var idProperty = entityType.FindProperty("Id");
            if (idProperty != null && idProperty.ClrType == typeof(Guid))
            {
                // Mark as generated on add (enables client-side generation by default)
                idProperty.ValueGenerated = ValueGenerated.OnAdd;

                // Use server-side NEWID() only for SQL Server
                if (Database.IsSqlServer())
                {
                    idProperty.SetDefaultValueSql("NEWID()");
                }
                else if (Database.IsSqlite())
                {
                    // Set no Default. This is imortant for Integration Tests to work.
                }
            }
        }
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
