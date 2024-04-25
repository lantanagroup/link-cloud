using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LantanaGroup.Link.DataAcquisition.Domain;

public class DataAcquisitionDbContext : DbContext
{
    public DataAcquisitionDbContext(DbContextOptions<DataAcquisitionDbContext> options) : base(options)
    {
    }

    public DbSet<FhirQueryConfiguration> FhirQueryConfigurations { get; set; }
    public DbSet<FhirListConfiguration> FhirListConfigurations { get; set; }
    public DbSet<QueriedFhirResourceRecord> QueriedFhirResources { get; set; }
    public DbSet<QueryPlan> QueryPlan { get; set; }
    public DbSet<ReferenceResources> ReferenceResources { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //-------------------QueryPlan-------------------

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
            .Property(b => b.Authentication)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<AuthenticationConfiguration>(v, new JsonSerializerOptions())
        );

        //-------------------FhirListConfiguration-------------------
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
    }
}
