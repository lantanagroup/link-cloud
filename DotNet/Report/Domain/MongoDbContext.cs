using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.SerDes;
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
    public DbSet<ReportEntry> ReportEntries { get; set; } = null!;
    public DbSet<ReportResource> ReportResources { get; set; } = null!;
    public DbSet<ReportPopulation> ReportPopulations { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ReportSchedule>()
            .ToCollection("reportSchedule");

        modelBuilder.Entity<ReportEntry>()
            .ToCollection("reportEntry");

        modelBuilder.Entity<ReportResource>()
            .ToCollection("reportResource");

        modelBuilder.Entity<ReportPopulation>()
            .ToCollection("reportPopulation");

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

        // Indexes for ReportEntry
        modelBuilder.Entity<ReportEntry>()
            .HasIndex(x => new { x.FacilityId, x.ReportScheduleId, x.PatientId });

        modelBuilder.Entity<ReportEntry>()
            .HasIndex(x => new { x.ReportScheduleId, x.ReportingStatus });

        modelBuilder.Entity<ReportEntry>()
            .HasIndex(x => new { x.FacilityId, x.PatientId });

        modelBuilder.Entity<ReportEntry>()
            .HasIndex(x => new { x.ReportingStatus, x.SubmissionStatus });

        modelBuilder.Entity<ReportEntry>()
            .HasIndex(x => x.ReportScheduleId);

        modelBuilder.Entity<ReportEntry>()
            .HasIndex(x => x.CreateDate)
            .IsDescending();

        // Indexes for ReportResource
        modelBuilder.Entity<ReportResource>()
            .HasIndex(x => new { x.FacilityId, x.ResourceType, x.ResourceId });

        modelBuilder.Entity<ReportResource>()
            .HasIndex(x => new { x.FacilityId, x.PatientId });

        modelBuilder.Entity<ReportResource>()
            .HasIndex(x => x.ResourceType);

        // Indexes for ReportPopulation
        modelBuilder.Entity<ReportPopulation>()
            .HasIndex(x => new { x.FacilityId });

        modelBuilder.Entity<ReportPopulation>()
            .HasIndex(x => new { x.FacilityId, x.ReportScheduleId });

        modelBuilder.Entity<ReportPopulation>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.OwnsMany(e => e.GroupPopulationList, gp =>
            {
                gp.Property(p => p.PopulationCode)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, LinkFhirSerializerOptions.ForFhirLenientSerialization),
                        v => JsonSerializer.Deserialize<CodeableConcept>(v, LinkFhirSerializerOptions.ForFhirLenientSerialization)!);

                gp.OwnsMany(g => g.GroupPopulationMeasureReportList);
            });
        });
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

            // Indexes for reportEntry collection
            var reportEntryCollection = MongoDatabase.GetCollection<ReportEntry>("reportEntry");
            var reportEntryBuilders = Builders<ReportEntry>.IndexKeys;
            await reportEntryCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReportEntry>(reportEntryBuilders.Ascending(x => x.FacilityId).Ascending(x => x.ReportScheduleId).Ascending(x => x.PatientId), indexOptions),
                cancellationToken: cancellationToken);
            await reportEntryCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReportEntry>(reportEntryBuilders.Ascending(x => x.ReportScheduleId).Ascending(x => x.ReportingStatus), indexOptions),
                cancellationToken: cancellationToken);
            await reportEntryCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReportEntry>(reportEntryBuilders.Ascending(x => x.FacilityId).Ascending(x => x.PatientId), indexOptions),
                cancellationToken: cancellationToken);
            await reportEntryCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReportEntry>(reportEntryBuilders.Ascending(x => x.ReportingStatus).Ascending(x => x.SubmissionStatus), indexOptions),
                cancellationToken: cancellationToken);
            await reportEntryCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReportEntry>(reportEntryBuilders.Ascending(x => x.ReportScheduleId), indexOptions),
                cancellationToken: cancellationToken);
            await reportEntryCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReportEntry>(reportEntryBuilders.Descending(x => x.CreateDate), indexOptions),
                cancellationToken: cancellationToken);

            // Indexes for reportResource collection
            var reportResourceCollection = MongoDatabase.GetCollection<ReportResource>("reportResource");
            var reportResourceBuilders = Builders<ReportResource>.IndexKeys;
            await reportResourceCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReportResource>(reportResourceBuilders.Ascending(x => x.FacilityId).Ascending(x => x.ResourceType).Ascending(x => x.ResourceId), indexOptions),
                cancellationToken: cancellationToken);
            await reportResourceCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReportResource>(reportResourceBuilders.Ascending(x => x.FacilityId).Ascending(x => x.PatientId), indexOptions),
                cancellationToken: cancellationToken);
            await reportResourceCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReportResource>(reportResourceBuilders.Ascending(x => x.ResourceType), indexOptions),
                cancellationToken: cancellationToken);

            // Indexes for reportPopulation collection
            var reportPopulationCollection = MongoDatabase.GetCollection<ReportPopulation>("reportPopulation");
            var reportPopulationBuilders = Builders <ReportPopulation>.IndexKeys;
            await reportPopulationCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReportPopulation>(reportPopulationBuilders.Ascending(x => x.FacilityId), indexOptions),
                cancellationToken: cancellationToken);
            await reportPopulationCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ReportPopulation>(reportPopulationBuilders.Ascending(x => x.FacilityId).Ascending(x => x.ReportScheduleId), indexOptions),
                cancellationToken: cancellationToken);
        }
        catch(Exception ex)
        {
            _logger.LogWarning(ex, "Exception While Creating Mongo Indexes");
        }
    }
}