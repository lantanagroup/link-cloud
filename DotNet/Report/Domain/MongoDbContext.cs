using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Entities;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.EntityFrameworkCore.Extensions;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Domain;

public class MongoDbContext : DbContext
{
    public IMongoDatabase MongoDatabase { get; }

    public MongoDbContext(DbContextOptions<MongoDbContext> options, IMongoDatabase mongoDatabase)
        : base(options)
    {
        MongoDatabase = mongoDatabase;
    }

    public DbSet<ReportSchedule> ReportSchedules { get; set; } = null!;
    public DbSet<PatientSubmissionEntry> PatientSubmissionEntries { get; set; } = null!;
    public DbSet<FhirResource> FhirResources { get; set; } = null!;
    public DbSet<ReportScheduleResourceMap> ReportScheduleResourceMaps { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ReportSchedule>()
            .ToCollection("reportSchedule");

        modelBuilder.Entity<PatientSubmissionEntry>()
            .ToCollection("patientSubmissionEntry");

        modelBuilder.Entity<FhirResource>()
            .ToCollection("fhirResource");

        modelBuilder.Entity<ReportScheduleResourceMap>()
            .ToCollection("reportScheduleResourceMap");

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
    }
}