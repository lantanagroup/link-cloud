using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Entities;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Extensions;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Domain;

public class MongoDbContext : DbContext
{
    public IMongoDatabase MongoDatabase { get; }
    private readonly ILogger<MongoDbContext> _logger;

    public MongoDbContext(DbContextOptions<MongoDbContext> options, IMongoDatabase mongoDatabase, ILogger<MongoDbContext> logger)
        : base(options)
    {
        MongoDatabase = mongoDatabase;
        _logger = logger;

        Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
    }

    public DbSet<ReportSchedule> ReportSchedules { get; set; } = null!;
    public DbSet<PatientSubmissionEntry> PatientSubmissionEntries { get; set; } = null!;
    public DbSet<FhirResource> FhirResources { get; set; } = null!;
    public DbSet<PatientSubmissionEntryResourceMap> PatientEntryResourceMaps { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ReportSchedule>()
            .ToCollection("reportSchedule");

        modelBuilder.Entity<PatientSubmissionEntry>()
            .ToCollection("patientSubmissionEntry");

        modelBuilder.Entity<FhirResource>()
            .ToCollection("fhirResource");

        modelBuilder.Entity<PatientSubmissionEntryResourceMap>()
            .ToCollection("patientSubmissionEntryResourceMap");

        // Configure FHIR Resource properties with value converters for JSON serialization
        var fhirJsonOptions = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector, new FhirJsonPocoDeserializerSettings { Validator = null });

        modelBuilder.Entity<FhirResource>()
            .Property(p => p.Resource)
            .HasConversion(
                v => JsonSerializer.Serialize(v, fhirJsonOptions),
                v => JsonSerializer.Deserialize<Resource>(v, fhirJsonOptions)!);

        modelBuilder.Entity<PatientSubmissionEntry>()
            .Property(e => e.MeasureReport)
            .HasConversion(
                v => JsonSerializer.Serialize(v, fhirJsonOptions),
                v => JsonSerializer.Deserialize<MeasureReport>(v, fhirJsonOptions));

        // Indexes for ReportSchedule
        modelBuilder.Entity<ReportSchedule>()
            .HasIndex(x => x.FacilityId);

        modelBuilder.Entity<ReportSchedule>()
            .HasIndex(x => new { x.FacilityId, x.Id });

        modelBuilder.Entity<ReportSchedule>()
            .HasIndex(x => new { x.FacilityId, x.ReportStartDate, x.ReportEndDate });

        modelBuilder.Entity<ReportSchedule>()
            .HasIndex(x => x.Status);

        modelBuilder.Entity<ReportSchedule>()
            .HasIndex(x => x.CreateDate)
            .IsDescending();

        // Indexes for PatientSubmissionEntry
        modelBuilder.Entity<PatientSubmissionEntry>()
            .HasIndex(x => new { x.FacilityId, x.ReportScheduleId, x.PatientId, x.ReportType });

        modelBuilder.Entity<PatientSubmissionEntry>()
            .HasIndex(x => new { x.ReportScheduleId, x.Status });

        modelBuilder.Entity<PatientSubmissionEntry>()
            .HasIndex(x => new { x.FacilityId, x.PatientId });

        modelBuilder.Entity<PatientSubmissionEntry>()
            .HasIndex(x => new { x.Status, x.ValidationStatus });

        modelBuilder.Entity<PatientSubmissionEntry>()
            .HasIndex(x => x.ReportScheduleId);

        modelBuilder.Entity<PatientSubmissionEntry>()
            .HasIndex(x => x.CreateDate)
            .IsDescending();

        // Indexes for FhirResource
        modelBuilder.Entity<FhirResource>()
            .HasIndex(x => new { x.FacilityId, x.ResourceType, x.ResourceId });

        modelBuilder.Entity<FhirResource>()
            .HasIndex(x => new { x.FacilityId, x.PatientId });

        modelBuilder.Entity<FhirResource>()
            .HasIndex(x => x.ResourceType);

        modelBuilder.Entity<FhirResource>()
            .HasIndex(x => x.ResourceCategoryType);

        // Indexes for PatientSubmissionEntryResourceMap
        modelBuilder.Entity<PatientSubmissionEntryResourceMap>()
            .HasIndex(x => new { x.ReportScheduleId, x.SubmissionEntryId, x.ResourceType, x.ResourceId });

        modelBuilder.Entity<PatientSubmissionEntryResourceMap>()
            .HasIndex(x => x.ReportScheduleId);

        modelBuilder.Entity<PatientSubmissionEntryResourceMap>()
            .HasIndex(x => new { x.SubmissionEntryId, x.FhirResourceId });

        modelBuilder.Entity<PatientSubmissionEntryResourceMap>()
            .HasIndex(x => x.ReportTypes);
    }

    /// <summary>
    /// Ensures all defined indexes exist by attempting to create them.
    /// MongoDB will skip creation if an index with the same key specification already exists.
    /// Call this method during application startup (e.g., from an IHostedService).
    /// </summary>
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var indexOptions = new CreateIndexOptions { Background = true }; // Build in background to avoid blocking

            // Indexes for reportSchedule collection
            var reportScheduleCollection = MongoDatabase.GetCollection<ReportSchedule>("reportSchedule");
            var reportScheduleBuilders = Builders<ReportSchedule>.IndexKeys;
            await reportScheduleCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReportSchedule>(reportScheduleBuilders.Ascending(x => x.FacilityId), indexOptions),
                cancellationToken: cancellationToken);
            await reportScheduleCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReportSchedule>(reportScheduleBuilders.Ascending(x => x.FacilityId).Ascending(x => x.Id), indexOptions),
                cancellationToken: cancellationToken);
            await reportScheduleCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReportSchedule>(reportScheduleBuilders.Ascending(x => x.FacilityId).Ascending(x => x.ReportStartDate).Ascending(x => x.ReportEndDate), indexOptions),
                cancellationToken: cancellationToken);
            await reportScheduleCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReportSchedule>(reportScheduleBuilders.Ascending(x => x.Status), indexOptions),
                cancellationToken: cancellationToken);
            await reportScheduleCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReportSchedule>(reportScheduleBuilders.Descending(x => x.CreateDate), indexOptions),
                cancellationToken: cancellationToken);

            // Indexes for patientSubmissionEntry collection
            var patientSubmissionEntryCollection = MongoDatabase.GetCollection<PatientSubmissionEntry>("patientSubmissionEntry");
            var patientSubmissionEntryBuilders = Builders<PatientSubmissionEntry>.IndexKeys;
            await patientSubmissionEntryCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<PatientSubmissionEntry>(patientSubmissionEntryBuilders.Ascending(x => x.FacilityId).Ascending(x => x.ReportScheduleId).Ascending(x => x.PatientId).Ascending(x => x.ReportType), indexOptions),
                cancellationToken: cancellationToken);
            await patientSubmissionEntryCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<PatientSubmissionEntry>(patientSubmissionEntryBuilders.Ascending(x => x.ReportScheduleId).Ascending(x => x.Status), indexOptions),
                cancellationToken: cancellationToken);
            await patientSubmissionEntryCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<PatientSubmissionEntry>(patientSubmissionEntryBuilders.Ascending(x => x.FacilityId).Ascending(x => x.PatientId), indexOptions),
                cancellationToken: cancellationToken);
            await patientSubmissionEntryCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<PatientSubmissionEntry>(patientSubmissionEntryBuilders.Ascending(x => x.Status).Ascending(x => x.ValidationStatus), indexOptions),
                cancellationToken: cancellationToken);
            await patientSubmissionEntryCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<PatientSubmissionEntry>(patientSubmissionEntryBuilders.Ascending(x => x.ReportScheduleId), indexOptions),
                cancellationToken: cancellationToken);
            await patientSubmissionEntryCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<PatientSubmissionEntry>(patientSubmissionEntryBuilders.Descending(x => x.CreateDate), indexOptions),
                cancellationToken: cancellationToken);

            // Indexes for fhirResource collection
            var fhirResourceCollection = MongoDatabase.GetCollection<FhirResource>("fhirResource");
            var fhirResourceBuilders = Builders<FhirResource>.IndexKeys;
            await fhirResourceCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<FhirResource>(fhirResourceBuilders.Ascending(x => x.FacilityId).Ascending(x => x.ResourceType).Ascending(x => x.ResourceId), indexOptions),
                cancellationToken: cancellationToken);
            await fhirResourceCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<FhirResource>(fhirResourceBuilders.Ascending(x => x.FacilityId).Ascending(x => x.PatientId), indexOptions),
                cancellationToken: cancellationToken);
            await fhirResourceCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<FhirResource>(fhirResourceBuilders.Ascending(x => x.ResourceType), indexOptions),
                cancellationToken: cancellationToken);
            await fhirResourceCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<FhirResource>(fhirResourceBuilders.Ascending(x => x.ResourceCategoryType), indexOptions),
                cancellationToken: cancellationToken);

            // Indexes for patientSubmissionEntryResourceMap collection
            var patientEntryResourceMapCollection = MongoDatabase.GetCollection<PatientSubmissionEntryResourceMap>("patientSubmissionEntryResourceMap");
            var patientEntryResourceMapBuilders = Builders<PatientSubmissionEntryResourceMap>.IndexKeys;
            await patientEntryResourceMapCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<PatientSubmissionEntryResourceMap>(patientEntryResourceMapBuilders.Ascending(x => x.ReportScheduleId).Ascending(x => x.SubmissionEntryId).Ascending(x => x.ResourceType).Ascending(x => x.ResourceId), indexOptions),
                cancellationToken: cancellationToken);
            await patientEntryResourceMapCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<PatientSubmissionEntryResourceMap>(patientEntryResourceMapBuilders.Ascending(x => x.ReportScheduleId), indexOptions),
                cancellationToken: cancellationToken);
            await patientEntryResourceMapCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<PatientSubmissionEntryResourceMap>(patientEntryResourceMapBuilders.Ascending(x => x.SubmissionEntryId).Ascending(x => x.FhirResourceId), indexOptions),
                cancellationToken: cancellationToken);
            await patientEntryResourceMapCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<PatientSubmissionEntryResourceMap>(patientEntryResourceMapBuilders.Ascending(x => x.ReportTypes), indexOptions),
                cancellationToken: cancellationToken);
        }
        catch(Exception ex)
        {
            _logger.LogWarning(ex, "Exception While Creating Mongo Indexes");
        }
    }
}