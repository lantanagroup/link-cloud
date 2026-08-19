using LantanaGroup.Link.MockDmrpApi.Domain.Context.Mappings;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LantanaGroup.Link.MockDmrpApi.Domain.Context;

public class ReportingPlanDbContext : DbContext
{
    public ReportingPlanDbContext(DbContextOptions<ReportingPlanDbContext> options) : base(options)
    {
    }

    public virtual DbSet<ReportingPlanEntryEntity> ReportingPlanEntries { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ReportingPlanEntryMap());
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>
/// Lets <c>dotnet ef</c> construct the context without starting the host. Never used at runtime.
/// </summary>
/// <remarks>
/// EF prefers this factory over the application's service provider, so the connection
/// string here is the one <c>dotnet ef database update</c> will actually target. It reads
/// <c>ConnectionStrings__DatabaseConnection</c> first so that pointing the tooling at a
/// real server is just an environment variable:
/// <code>
/// ConnectionStrings__DatabaseConnection="Server=localhost,1433;Initial Catalog=link-mock-dmrp;..." \
///   dotnet ef database update --project MockDmrpApi.csproj --startup-project MockDmrpApi.csproj
/// </code>
/// Without it, the local default below applies. Scaffolding a migration only needs the
/// provider, so the value does not have to point at a reachable server for
/// <c>migrations add</c>.
/// </remarks>
public class ReportingPlanDbContextFactory : IDesignTimeDbContextFactory<ReportingPlanDbContext>
{
    private const string LocalDefault =
        "Server=localhost\\SQLEXPRESS;Initial Catalog=link-mock-dmrp;Integrated Security=true;Encrypt=false;TrustServerCertificate=True";

    public ReportingPlanDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DatabaseConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = LocalDefault;
        }

        var optionsBuilder = new DbContextOptionsBuilder<ReportingPlanDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ReportingPlanDbContext(optionsBuilder.Options);
    }
}
