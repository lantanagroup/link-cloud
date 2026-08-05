using LantanaGroup.Link.MockDmrpApi.Application.Services;
using LantanaGroup.Link.MockDmrpApi.Domain.Context;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ServiceTests globally imports Hl7.Fhir.Model, which has its own Task type.
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.MockDmrpApi;

/// <summary>
/// Hosts the reporting plan service over a real relational database.
/// </summary>
/// <remarks>
/// SQLite rather than SQL Server: these tests are about behaviour the in-memory fake cannot
/// show -- the save interceptor actually running, translated LINQ actually executing, and
/// the unique index actually rejecting a duplicate. SQLite enforces unique indexes, so the
/// constraint tests are meaningful.
/// <para>
/// Note this uses EnsureCreated rather than Migrate, so the EF migration itself is not
/// exercised here. That is covered by bringing the service up against SQL Server.
/// </para>
/// </remarks>
public class MockDmrpApiIntegrationTestFixture : IDisposable
{
    private readonly string _databasePath;
    private readonly ServiceProvider _serviceProvider;

    public MockDmrpApiIntegrationTestFixture()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"mockdmrp_{Guid.NewGuid():N}.db");

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        builder.Services.AddSingleton<UpdateBaseEntityInterceptor>();

        builder.Services.AddDbContext<ReportingPlanDbContext>((sp, options) =>
            options.UseSqlite($"Data Source={_databasePath}")
                   .AddInterceptors(sp.GetRequiredService<UpdateBaseEntityInterceptor>()));

        builder.Services.AddScoped<IBaseEntityRepository<ReportingPlanEntryEntity>,
                                   BaseEntityRepository<ReportingPlanEntryEntity, ReportingPlanDbContext>>();
        builder.Services.AddScoped<IReportingPlanService, ReportingPlanService>();

        _serviceProvider = builder.Services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ReportingPlanDbContext>().Database.EnsureCreated();
    }

    public IServiceScope CreateScope() => _serviceProvider.CreateScope();

    /// <summary>Empties the table so each test starts from a known state.</summary>
    public async Task ResetAsync()
    {
        using var scope = CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportingPlanDbContext>();
        context.ReportingPlanEntries.RemoveRange(context.ReportingPlanEntries);
        await context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();

        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
            // A leftover temp file is not worth failing a test run over.
        }

        GC.SuppressFinalize(this);
    }
}
