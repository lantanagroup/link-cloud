using AppAny.Quartz.EntityFrameworkCore.Migrations;
using AppAny.Quartz.EntityFrameworkCore.Migrations.SqlServer;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResourceType = Hl7.Fhir.Model.ResourceType;


namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;

public class DataAcquisitionDbContext : DbContext
{
    public DataAcquisitionDbContext(DbContextOptions<DataAcquisitionDbContext> options) : base(options)
    {
    }

    public DbSet<OrganizationLocationCondition> LocationConditions { get; set; }
    public DbSet<OrganizationLocationConfiguration> LocationConfigurations { get; set; }
    public virtual DbSet<OrganizationLocationMapping> OrganizationLocationMappings { get; set; }
    public DbSet<EncounterMapping> EncounterMappings { get; set; }
    public DbSet<EncounterLocation> EncounterLocations { get; set; }

    public DbSet<FhirQueryConfiguration> FhirQueryConfigurations { get; set; }
    public DbSet<FhirListConfiguration> FhirListConfigurations { get; set; }
    public DbSet<QueryPlan> QueryPlans { get; set; }
    public DbSet<ReferenceResources> ReferenceResources { get; set; }
    public DbSet<FhirQuery> FhirQueries { get; set; }
    public virtual DbSet<FhirQueryResourceType> FhirQueryResourceTypes { get; set; }
    public DbSet<DataAcquisitionLog> DataAcquisitionLogs { get; set; }
    public DbSet<SftpAcquisitionLog> SftpAcquisitionLogs { get; set; }
    public DbSet<SftpConfiguration> SftpConfigurations { get; set; }
    public DbSet<DataAcquisitionLogReferenceResource> DataAcquisitionLogReferenceResources { get; set; }
    public DbSet<DataAcquisitionLogNote> DataAcquisitionLogNotes { get; set; }
    public DbSet<DataAcquisitionLogResourceId> DataAcquisitionLogResourceIds { get; set; }
    public DbSet<ScheduledReportEntity> ScheduledReports { get; set; }
    public DbSet<PendingReferenceId> PendingReferenceIds { get; set; }

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

            var queryConfigComparer = new ValueComparer<Dictionary<string, IQueryConfig>>(
                (c1, c2) => JsonSerializer.Serialize(c1, jsonOptions) == JsonSerializer.Serialize(c2, jsonOptions),
                c => c == null ? 0 : JsonSerializer.Serialize(c, jsonOptions).GetHashCode(),
                c => JsonSerializer.Deserialize<Dictionary<string, IQueryConfig>>(JsonSerializer.Serialize(c, jsonOptions), jsonOptions));

            entity.Property(b => b.InitialQueries)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonOptions),
                    v => JsonSerializer.Deserialize<Dictionary<string, IQueryConfig>>(v, jsonOptions))
                .Metadata.SetValueComparer(queryConfigComparer);

            entity.Property(b => b.SupplementalQueries)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, jsonOptions),
                        v => JsonSerializer.Deserialize<Dictionary<string, IQueryConfig>>(v, jsonOptions))
                    .Metadata.SetValueComparer(queryConfigComparer);
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

            var ehrPatientListComparer = new ValueComparer<List<EhrPatientList>>(
                (c1, c2) => c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList());

            entity.Property(p => p.EHRPatientLists)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }),
                v => JsonSerializer.Deserialize<List<EhrPatientList>>(v, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }))
            .Metadata.SetValueComparer(ehrPatientListComparer);
        });

        //-------------------ReferenceResources-------------------
        modelBuilder.Entity<ReferenceResources>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.QueryPhase).HasConversion(new EnumToStringConverter<QueryPhase>());
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
                    .HasPrincipalKey(q => q.Id)
                    .HasConstraintName("FK_FhirQueryResourceType_FhirQuery");
        });

        modelBuilder.Entity<FhirQueryResourceType>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.ResourceType).HasConversion(new EnumToStringConverter<ResourceType>());
        });

        //-------------------DataAcquisitionLog-------------------
        modelBuilder.Entity<DataAcquisitionLog>(entity =>
        {
            entity.Property(b => b.Id).ValueGeneratedOnAdd();

            entity.HasMany(x => x.FhirQueries)
            .WithOne(x => x.DataAcquisitionLog)
            .HasForeignKey(x => x.DataAcquisitionLogId)
            .HasPrincipalKey(x => x.Id);

            entity.HasMany(x => x.NoteEntries)
                .WithOne(x => x.DataAcquisitionLog)
                .HasForeignKey(x => x.DataAcquisitionLogId)
                .HasPrincipalKey(x => x.Id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ScheduledReportEntity)
                .WithMany(x => x.DataAcquisitionLogs)
                .HasForeignKey(x => x.ReportTrackingId)
                .HasPrincipalKey(x => x.ReportTrackingId)
                .OnDelete(DeleteBehavior.SetNull);

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

            entity.HasIndex(e => new { e.FacilityId, e.CorrelationId, e.QueryPhase, e.ReferenceResourceType })
                .IsUnique()
                .HasDatabaseName("UX_DataAcquisitionLogs_ReferenceLogKey")
                .HasFilter("[CorrelationId] IS NOT NULL AND [QueryPhase] IS NOT NULL AND [ReferenceResourceType] IS NOT NULL");

            // Covers GetResourceIdsForReportPatient and sibling EXISTS checks in
            // GetNextEligibleBatchForFacility. The unique ReferenceLogKey index above is
            // filtered to ReferenceResourceType IS NOT NULL, so regular query logs miss it.
            entity.HasIndex(e => new { e.FacilityId, e.CorrelationId })
                .HasDatabaseName("IX_DataAcquisitionLogs_FacilityId_CorrelationId")
                .IncludeProperties(
                    nameof(DataAcquisitionLog.QueryPhase),
                    nameof(DataAcquisitionLog.Status),
                    nameof(DataAcquisitionLog.ReportTrackingId));

            entity.HasIndex(e => new { e.ExecutionDate, e.Id })
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
                    nameof(DataAcquisitionLog.CompletionTimeMilliseconds)
                );

            entity.HasIndex(e => new { e.Status, e.ModifyDate })
                .HasDatabaseName("IX_DataAcquisitionLogs_Status_ModifyDate");

            entity.HasIndex(e => new { e.Status, e.ExecutionDate })
                .HasDatabaseName("IX_DataAcquisitionLogs_Status_ExecutionDate");

            entity.HasIndex(e => e.FacilityId)
                .HasDatabaseName("IX_DataAcquisitionLogs_FacilityId_IsDeleted")
                .HasFilter("[IsDeleted] = 1");

            entity.HasIndex(e => new { e.TailSent, e.FacilityId, e.ReportTrackingId, e.CorrelationId, e.QueryPhase })
                .HasDatabaseName("IX_DataAcquisitionLogs_Tailing_Optimization")
                .HasFilter("[TailSent] = 0 AND [ReportTrackingId] IS NOT NULL AND [CorrelationId] IS NOT NULL");

            entity.HasIndex(e => new { e.TailSent, e.SiblingCount, e.FacilityId, e.CorrelationId, e.QueryPhase, e.Status })
                .HasDatabaseName("IX_DataAcquisitionLogs_InlineTail")
                .HasFilter("[TailSent] = 0 AND [SiblingCount] IS NOT NULL AND [CorrelationId] IS NOT NULL AND [QueryPhase] IS NOT NULL");

            // Covers GetReportSummaryAsync and other queries that aggregate by ReportTrackingId.
            // Without this, those queries do a full table scan and time out under load.
            entity.HasIndex(e => new { e.ReportTrackingId, e.IsDeleted })
                .HasDatabaseName("IX_DataAcquisitionLogs_ReportTrackingId_IsDeleted")
                .IncludeProperties(
                    nameof(DataAcquisitionLog.PatientId),
                    nameof(DataAcquisitionLog.Status),
                    nameof(DataAcquisitionLog.RetryAttempts),
                    nameof(DataAcquisitionLog.CompletionTimeMilliseconds)
                );

            // Covers GetTailingMessages Phase-1 query (filters !TailSent, checks Status, groups by facility/tracking/correlation/phase).
            entity.HasIndex(e => new { e.TailSent, e.Status })
                .HasDatabaseName("IX_DataAcquisitionLogs_TailSent_Status")
                .IncludeProperties(
                    nameof(DataAcquisitionLog.FacilityId),
                    nameof(DataAcquisitionLog.ReportTrackingId),
                    nameof(DataAcquisitionLog.CorrelationId),
                    nameof(DataAcquisitionLog.QueryPhase),
                    nameof(DataAcquisitionLog.TraceId),
                    nameof(DataAcquisitionLog.PatientId),
                    nameof(DataAcquisitionLog.ReportableEvent)
                );

            // Covers default UI pagination on (IsDeleted, Id DESC).
            entity.HasIndex(e => new { e.IsDeleted, e.Id })
                .HasDatabaseName("IX_DataAcquisitionLogs_IsDeleted_Id")
                .IncludeProperties(
                    nameof(DataAcquisitionLog.Priority),
                    nameof(DataAcquisitionLog.FacilityId),
                    nameof(DataAcquisitionLog.PatientId),
                    nameof(DataAcquisitionLog.ReportTrackingId),
                    nameof(DataAcquisitionLog.FhirVersion),
                    nameof(DataAcquisitionLog.QueryType),
                    nameof(DataAcquisitionLog.QueryPhase),
                    nameof(DataAcquisitionLog.ExecutionDate),
                    nameof(DataAcquisitionLog.CreateDate),
                    nameof(DataAcquisitionLog.RetryAttempts),
                    nameof(DataAcquisitionLog.Status)
                );

            });

        modelBuilder.Entity<PendingReferenceId>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.HasOne(e => e.DataAcquisitionLog)
                .WithMany()
                .HasForeignKey(e => e.DataAcquisitionLogId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_PendingReferenceIds_DataAcquisitionLog");
        });

            //-------------------DataAcquisitionLogResourceId-------------------
            modelBuilder.Entity<DataAcquisitionLogResourceId>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.HasIndex(e => e.DataAcquisitionLogId)
                    .HasDatabaseName("IX_DataAcquisitionLogResourceIds_DataAcquisitionLogId");
            });

            //-------------------ResourceReferenceType-------------------
        modelBuilder.Entity<ResourceReferenceType>()
            .Property(b => b.Id).ValueGeneratedOnAdd();

        modelBuilder.Entity<ResourceReferenceType>()
            .Property(b => b.QueryPhase)
            .HasConversion(new EnumToStringConverter<QueryPhase>());

        //-------------------DataAcquisitionLogNote-------------------
        modelBuilder.Entity<DataAcquisitionLogNote>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.HasIndex(e => e.DataAcquisitionLogId)
                .HasDatabaseName("IX_DataAcquisitionLogNotes_DataAcquisitionLogId");
        });

        //-------------------ScheduledReportEntity-------------------
        modelBuilder.Entity<ScheduledReportEntity>(entity =>
        {
            entity.HasKey(e => e.ReportTrackingId);

            entity.Property(e => e.Frequency)
                .HasConversion(new EnumToStringConverter<Frequency>());
        });

        //-------------------DataAcquisitionLogReferenceResource (junction via skip-navigation)-------------------
        modelBuilder.Entity<DataAcquisitionLog>()
            .HasMany(l => l.ReferenceResources)
            .WithMany(r => r.DataAcquisitionLogs)
            .UsingEntity<DataAcquisitionLogReferenceResource>(
                "DataAcquisitionLogReferenceResource",
                right => right.HasOne<ReferenceResources>()
                    .WithMany()
                    .HasForeignKey(j => j.ReferenceResourceId)
                    .OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne<DataAcquisitionLog>()
                    .WithMany()
                    .HasForeignKey(j => j.DataAcquisitionLogId)
                    .OnDelete(DeleteBehavior.Cascade),
                junction =>
                {
                    junction.HasKey(j => new { j.DataAcquisitionLogId, j.ReferenceResourceId });
                    junction.ToTable("DataAcquisitionLogReferenceResource");
                });

        //-------------------SftpAcquisitionLog-------------------
        modelBuilder.Entity<SftpAcquisitionLog>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.FacilityId)
                .IsRequired()
                .HasMaxLength(DataAcquisitionConstants.DatabaseSettings.MaxFacilityIdLength);

            entity.HasIndex(i => i.FacilityId)
                .HasDatabaseName("IX_SftpAcquisitionLog_FacilityId");

            entity.HasIndex(i => i.ScheduledDate)
                .HasDatabaseName("IX_SftpAcquisitionLog_ScheduledDate");

            entity.Property(d => d.Status)
                .HasMaxLength(50)
                .HasConversion(new EnumToStringConverter<RequestStatus>());

            entity.Property(d => d.AcquisitionType)
                .HasMaxLength(50)
                .HasConversion(new EnumToStringConverter<SftpAcquisitionType>());

            entity.Property(d => d.SubType)
                .HasMaxLength(50)
                .HasConversion(new EnumToStringConverter<SftpAcquisitionSubType>());

            entity.Property(e => e.FileNames)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => v != null ? JsonSerializer.Deserialize<List<string>>(v, new JsonSerializerOptions()) ?? new List<string>() : new List<string>())
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            entity.Property(e => e.Notes)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => v != null ? JsonSerializer.Deserialize<List<string>>(v, new JsonSerializerOptions()) ?? new List<string>() : new List<string>())
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

            entity.Property(e => e.Benchmarks)
                .HasConversion(
                    v => v != null ? JsonSerializer.Serialize(v, new JsonSerializerOptions()) : null,
                    v => v != null ? JsonSerializer.Deserialize<List<SftpAcquisitionBenchmark>>(v, new JsonSerializerOptions()) : null)
                .Metadata.SetValueComparer(new ValueComparer<List<SftpAcquisitionBenchmark>?>(
                    (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                    c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c == null ? null : c.ToList()));
        });

        //-------------------SftpConfiguration-------------------
        modelBuilder.Entity<SftpConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.OrganizationId)
                .IsRequired()
                .HasMaxLength(DataAcquisitionConstants.DatabaseSettings.MaxFacilityIdLength);

            entity.Property(e => e.Host)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.RemoteDirectory)
                .HasMaxLength(4096);

            var jsonOptions = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            };

            entity.Property(e => e.AcquisitionConfigurations)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonOptions),
                    v => v != null
                        ? JsonSerializer.Deserialize<List<SftpAcquisitionTypeConfiguration>>(v, jsonOptions) ?? new List<SftpAcquisitionTypeConfiguration>()
                        : new List<SftpAcquisitionTypeConfiguration>())
                .Metadata.SetValueComparer(new ValueComparer<List<SftpAcquisitionTypeConfiguration>>(
                    (c1, c2) => c1 != null && c2 != null && JsonSerializer.Serialize(c1, jsonOptions) == JsonSerializer.Serialize(c2, jsonOptions),
                    c => JsonSerializer.Serialize(c, jsonOptions).GetHashCode(),
                    c => JsonSerializer.Deserialize<List<SftpAcquisitionTypeConfiguration>>(JsonSerializer.Serialize(c, jsonOptions), jsonOptions) ?? new List<SftpAcquisitionTypeConfiguration>()));
        });


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

            // Supports the orphan-adoption backfill (SetPartOfIdForChildrenAsync), which filters on
            // (FacilityId, PartOfValue) for rows whose PartOfId is not yet resolved. Filtered to the
            // unresolved rows so the index stays small and the update is a seek rather than a scan.
            entity.HasIndex(e => new { e.FacilityId, e.PartOfValue }, "IX_LocationMapping_FacilityId_PartOfValue")
                .HasFilter("([PartOfId] IS NULL)");

            entity.Property(e => e.CreateDate).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ModifiedDate).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.PartOf).WithMany(p => p.InversePartOf).HasConstraintName("FK_LocationMapping_PartOf");
        });

        //-------------------EncounterMapping-------------------
        modelBuilder.Entity<EncounterMapping>(entity =>
        {
            entity.HasKey(e => e.EncounterMappingId).HasName("PK_EncounterMapping_EncounterMappingId");

            entity.Property(e => e.CreateDate).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.ModifiedDate).HasDefaultValueSql("(getutcdate())");
        });

        //-------------------EncounterLocation-------------------
        modelBuilder.Entity<EncounterLocation>(entity =>
        {
            entity.HasKey(e => e.EncounterLocationId).HasName("PK_EncounterLocation_EncounterLocationId");

            entity.Property(e => e.CreateDate).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.ModifiedDate).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.EncounterMapping).WithMany(p => p.EncounterLocations)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_EncounterLocation_EncounterMapping");

            entity.HasOne(d => d.OrganizationLocationMapping).WithMany(p => p.EncounterLocations)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EncounterLocation_OrganizationLocationMapping");
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
