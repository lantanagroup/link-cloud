using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace LantanaGroup.Link.DataAcquisition.Domain;

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
