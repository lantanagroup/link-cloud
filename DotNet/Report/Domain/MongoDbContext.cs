using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Extensions;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Domain;

public class MongoDbContext : DbContext
{
    public virtual IMongoDatabase MongoDatabase { get; }
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

        modelBuilder.Entity<ReportPopulation>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.OwnsMany(e => e.GroupPopulations, gp =>
            {
                gp.Property(p => p.PopulationCode)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, LinkFhirSerializerOptions.ForFhirLenientSerialization),
                        v => JsonSerializer.Deserialize<CodeableConcept>(v, LinkFhirSerializerOptions.ForFhirLenientSerialization)!);

                gp.OwnsMany(g => g.MeasureReportPopulations);
            });
        });
    }

    /// <summary>
    /// Begins a new MongoDB multi-document transaction.
    /// </summary>
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        await Database.BeginTransactionAsync(cancellationToken);
    }

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        await Database.CommitTransactionAsync(cancellationToken);
    }

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        await Database.RollbackTransactionAsync(cancellationToken);
    }

    /// <summary>
    /// Ensures all indexes exist.
    /// </summary>
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var indexOptions = new CreateIndexOptions { Background = true };

            // ReportSchedule indexes
            var reportScheduleCollection = MongoDatabase.GetCollection<ReportSchedule>("reportSchedule");
            var rs = Builders<ReportSchedule>.IndexKeys;
            await SafeCreateIndex(reportScheduleCollection, new CreateIndexModel<ReportSchedule>(rs.Ascending(x => x.FacilityId), indexOptions), cancellationToken);
            await SafeCreateIndex(reportScheduleCollection, new CreateIndexModel<ReportSchedule>(rs.Ascending(x => x.FacilityId).Ascending(x => x.Id), indexOptions), cancellationToken);
            await SafeCreateIndex(reportScheduleCollection, new CreateIndexModel<ReportSchedule>(rs.Ascending(x => x.FacilityId).Ascending(x => x.ReportStartDate).Ascending(x => x.ReportEndDate), indexOptions), cancellationToken);
            await SafeCreateIndex(reportScheduleCollection, new CreateIndexModel<ReportSchedule>(rs.Ascending(x => x.Status), indexOptions), cancellationToken);
            await SafeCreateIndex(reportScheduleCollection, new CreateIndexModel<ReportSchedule>(rs.Descending(x => x.CreateDate), indexOptions), cancellationToken);

            // ReportEntry indexes
            var reportEntryCollection = MongoDatabase.GetCollection<ReportEntry>("reportEntry");
            var re = Builders<ReportEntry>.IndexKeys;
            await SafeCreateIndex(reportEntryCollection, new CreateIndexModel<ReportEntry>(re.Ascending(x => x.FacilityId).Ascending(x => x.ReportScheduleId).Ascending(x => x.PatientId), indexOptions), cancellationToken);
            await SafeCreateIndex(reportEntryCollection, new CreateIndexModel<ReportEntry>(re.Ascending(x => x.ReportScheduleId).Ascending(x => x.ReportingStatus), indexOptions), cancellationToken);
            await SafeCreateIndex(reportEntryCollection, new CreateIndexModel<ReportEntry>(re.Ascending(x => x.FacilityId).Ascending(x => x.PatientId), indexOptions), cancellationToken);
            await SafeCreateIndex(reportEntryCollection, new CreateIndexModel<ReportEntry>(re.Ascending(x => x.ReportingStatus).Ascending(x => x.SubmissionStatus), indexOptions), cancellationToken);
            await SafeCreateIndex(reportEntryCollection, new CreateIndexModel<ReportEntry>(re.Ascending(x => x.ReportScheduleId), indexOptions), cancellationToken);
            await SafeCreateIndex(reportEntryCollection, new CreateIndexModel<ReportEntry>(re.Descending(x => x.CreateDate), indexOptions), cancellationToken);

            // ReportResource indexes
            var reportResourceCollection = MongoDatabase.GetCollection<ReportResource>("reportResource");
            var rr = Builders<ReportResource>.IndexKeys;
            await SafeCreateIndex(reportResourceCollection, new CreateIndexModel<ReportResource>(rr.Ascending(x => x.FacilityId).Ascending(x => x.ResourceType).Ascending(x => x.ResourceId), indexOptions), cancellationToken);
            await SafeCreateIndex(reportResourceCollection, new CreateIndexModel<ReportResource>(rr.Ascending(x => x.FacilityId).Ascending(x => x.PatientId), indexOptions), cancellationToken);
            await SafeCreateIndex(reportResourceCollection, new CreateIndexModel<ReportResource>(rr.Ascending(x => x.ResourceType), indexOptions), cancellationToken);

            // ReportPopulation indexes
            var reportPopulationCollection = MongoDatabase.GetCollection<ReportPopulation>("reportPopulation");
            var rp = Builders<ReportPopulation>.IndexKeys;
            await SafeCreateIndex(reportPopulationCollection, new CreateIndexModel<ReportPopulation>(rp.Ascending(x => x.FacilityId), indexOptions), cancellationToken);
            await SafeCreateIndex(reportPopulationCollection, new CreateIndexModel<ReportPopulation>(rp.Ascending(x => x.FacilityId).Ascending(x => x.ReportScheduleId), indexOptions), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected exception while creating Mongo indexes");
        }
    }

    /// <summary>
    /// Helper that safely creates an index, or fails gracefully.
    /// </summary>
    private async Task SafeCreateIndex<T>(IMongoCollection<T> collection, CreateIndexModel<T> model, CancellationToken ct)
    {
        try
        {
            await collection.Indexes.CreateOneAsync(model, cancellationToken: ct);
        }
        catch (MongoCommandException ex)
            when (ex.Code == 13
               || ex.Message.Contains("unique index cannot be modified", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("Index already exists", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Index already exists or cannot be modified on collection {Collection} (Cosmos DB limitation) — skipping.",
                collection.CollectionNamespace.CollectionName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create index on collection {Collection}",
                collection.CollectionNamespace.CollectionName);
        }
    }
}