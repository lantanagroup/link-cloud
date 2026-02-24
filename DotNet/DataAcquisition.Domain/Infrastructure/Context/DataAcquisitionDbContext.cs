using System.Text.Json;
using System.Text.Json.Serialization;
using AppAny.Quartz.EntityFrameworkCore.Migrations;
using AppAny.Quartz.EntityFrameworkCore.Migrations.SqlServer;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using ScheduledReport = LantanaGroup.Link.Shared.Application.Models.ScheduledReport;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;

public class DataAcquisitionDbContext : DbContext
{
    public DataAcquisitionDbContext(DbContextOptions<DataAcquisitionDbContext> options) : base(options)
    {
    }

    public DbSet<OrganizationLocationCondition> LocationConditions { get; set; }
    public DbSet<OrganizationLocationConfiguration> LocationConfigurations { get; set; }
    public virtual DbSet<OrganizationLocationMapping> OrganizationLocationMappings { get; set; }

    public DbSet<FhirQueryConfiguration> FhirQueryConfigurations { get; set; }
    public DbSet<FhirListConfiguration> FhirListConfigurations { get; set; }
    public DbSet<QueryPlan> QueryPlans { get; set; }
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

            var jsonOptions = new JsonSerializerOptions();
            jsonOptions.Converters.Add(new QueryConfigConverter());
            jsonOptions.Converters.Add(new ParameterConverter());
            jsonOptions.Converters.Add(new JsonStringEnumConverter());

            entity.Property(b => b.InitialQueries)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonOptions),
                    v => JsonSerializer.Deserialize<Dictionary<string, IQueryConfig>>(v, jsonOptions));

            entity.Property(b => b.SupplementalQueries)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, jsonOptions),
                        v => JsonSerializer.Deserialize<Dictionary<string, IQueryConfig>>(v, jsonOptions));
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
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }),
                v => JsonSerializer.Deserialize<List<EhrPatientList>>(v, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }));
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

            entity.Property(e => e.ResourceType).HasConversion(new EnumToStringConverter<ResourceType>());

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

            entity.Property(d => d.Status)
                .HasConversion(new EnumToStringConverter<RequestStatus>())
                .HasMaxLength(50);

            entity.Property(d => d.Priority)
                .HasConversion(new EnumToStringConverter<AcquisitionPriority>())
                .HasMaxLength(50);

            entity.Property(d => d.QueryPhase)
                .HasConversion(new EnumToStringConverter<QueryPhase>())
                .HasMaxLength(50);

            entity.Property(d => d.QueryType)
                .HasConversion(new EnumToStringConverter<FhirQueryType>())
                .HasMaxLength(50);

            entity.HasIndex(e => new { e.ExecutionDate, e.Id })
                .IsDescending()
                .IsDescending()
                .HasDatabaseName("IX_DataAcquisitionLogs_Paging_Default")
                .IncludeProperties(
                    nameof(DataAcquisitionLog.Priority),
                    nameof(DataAcquisitionLog.FacilityId),
                    nameof(DataAcquisitionLog.IsCensus),
                    nameof(DataAcquisitionLog.PatientId),
                    nameof(DataAcquisitionLog.ReportableEvent),
                    nameof(DataAcquisitionLog.ReportTrackingId),
                    nameof(DataAcquisitionLog.CorrelationId),
                    nameof(DataAcquisitionLog.TraceId),
                    nameof(DataAcquisitionLog.FhirVersion),
                    nameof(DataAcquisitionLog.QueryType),
                    nameof(DataAcquisitionLog.QueryPhase),
                    nameof(DataAcquisitionLog.Status),
                    nameof(DataAcquisitionLog.RetryAttempts),
                    nameof(DataAcquisitionLog.CompletionDate),
                    nameof(DataAcquisitionLog.CompletionTimeMilliseconds)
                );

            entity.HasIndex(e => new { e.FacilityId, e.Status, e.ExecutionDate, e.Id })
                .HasDatabaseName("IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id")
                .IncludeProperties(
                    nameof(DataAcquisitionLog.Priority),
                    nameof(DataAcquisitionLog.IsCensus),
                    nameof(DataAcquisitionLog.PatientId),
                    nameof(DataAcquisitionLog.ReportableEvent),
                    nameof(DataAcquisitionLog.ReportTrackingId),
                    nameof(DataAcquisitionLog.CorrelationId),
                    nameof(DataAcquisitionLog.FhirVersion),
                    nameof(DataAcquisitionLog.QueryType),
                    nameof(DataAcquisitionLog.QueryPhase),
                    nameof(DataAcquisitionLog.TraceId),
                    nameof(DataAcquisitionLog.RetryAttempts),
                    nameof(DataAcquisitionLog.CompletionDate),
                    nameof(DataAcquisitionLog.CompletionTimeMilliseconds),
                    nameof(DataAcquisitionLog.ResourceAcquiredIds),
                    nameof(DataAcquisitionLog.Notes),
                    nameof(DataAcquisitionLog.ScheduledReport)
                );

            entity.HasIndex(e => new { e.TailSent, e.FacilityId, e.ReportTrackingId, e.CorrelationId, e.ReportStartDate, e.ReportEndDate, e.QueryPhase })
                .HasDatabaseName("IX_DataAcquisitionLogs_Tailing_Optimization")
                .HasFilter("[TailSent] = 0 AND [ReportTrackingId] IS NOT NULL AND [CorrelationId] IS NOT NULL AND [ReportStartDate] IS NOT NULL AND [ReportEndDate] IS NOT NULL");
        });

        //-------------------ResourceReferenceType-------------------
        modelBuilder.Entity<ResourceReferenceType>()
            .Property(b => b.Id).ValueGeneratedOnAdd();

        modelBuilder.Entity<ResourceReferenceType>()
            .Property(b => b.QueryPhase)
            .HasConversion(new EnumToStringConverter<QueryPhase>());

        //-------------------OrganizationLocationCondition-------------------
        modelBuilder.Entity<OrganizationLocationCondition>(entity =>
        {
            entity.HasKey(e => e.ConditionId).HasName("PK_LocationCondition_ConditionId");

            entity.Property(e => e.CreatedOn).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Priority).HasDefaultValue(1);
            entity.Property(e => e.ModifiedOn).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.Config).WithMany(p => p.LocationConditions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LocationCondition_ConfigId");
        });

        //-------------------OrganizationLocationConfiguration-------------------
        modelBuilder.Entity<OrganizationLocationConfiguration>(entity =>
        {
            entity.HasKey(e => e.ConfigId).HasName("PK_LocationConfiguration_ConfigId");

            entity.Property(e => e.FacilityId)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.CreatedOn).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ModifiedOn).HasDefaultValueSql("(getutcdate())");
        });

        //-------------------OrganizationLocationMapping-------------------
        modelBuilder.Entity<OrganizationLocationMapping>(entity =>
        {
            entity.HasKey(e => e.LocationMappingId).HasName("PK_OrganizationLocationMapping_LocationMappingId");

            entity.HasIndex(e => e.PartOfId, "IX_LocationMapping_PartOfId").HasFilter("([PartOfId] IS NOT NULL)");

            entity.Property(e => e.CreateDate).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ModifiedDate).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.PartOf).WithMany(p => p.InversePartOf).HasConstraintName("FK_LocationMapping_PartOf");
        });

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
