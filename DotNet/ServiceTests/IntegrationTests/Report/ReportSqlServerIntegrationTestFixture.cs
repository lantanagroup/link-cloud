using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Domain.Managers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report;

/// <summary>
/// A Report fixture backed by real SQL Server, for behavior the SQLite fixture cannot speak to.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ReportIntegrationTestFixture"/> runs on SQLite, which is fast but diverges from production
/// in ways that matter for storage-level guarantees: it raises <c>SqliteException</c> rather than
/// <see cref="SqlException"/> on a unique violation, ignores <c>MaxLength</c> entirely, is case-sensitive
/// by default where SQL Server usually is not, and locks the database file rather than a row.
/// </para>
/// <para>
/// It also applies migrations rather than <c>EnsureCreated()</c>, so the schema under test is the one a
/// deployment will actually get. A migration with a missing index or a wrong constraint passes an
/// <c>EnsureCreated</c> fixture silently, because that builds the schema from the model instead.
/// </para>
/// <para>
/// Deliberately narrow: it registers only what its tests need rather than mirroring the whole service.
/// Add to it when a test genuinely needs SQL Server, not by default — it costs a container start.
/// </para>
/// </remarks>
public class ReportSqlServerIntegrationTestFixture : IAsyncLifetime
{
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-CU13-ubuntu-22.04";

    // Points the fixture at an existing SQL Server instead of a Testcontainer, which drops startup from
    // ~30s to ~2s for the inner-dev loop. Mirrors the DataAcquisition fixture's switch.
    private static string? ExternalConnectionString =>
        Environment.GetEnvironmentVariable("LINK_TESTS_SQL_CONNECTION_STRING");

    private readonly MsSqlContainer? _sqlContainer;
    private IHost? _host;
    private string? _serverConnectionString;
    private string? _testDatabaseName;

    public IServiceScopeFactory ScopeFactory { get; private set; } = default!;

    public ReportSqlServerIntegrationTestFixture()
    {
        if (string.IsNullOrWhiteSpace(ExternalConnectionString))
        {
            _sqlContainer = new MsSqlBuilder(SqlServerImage).Build();
        }
    }

    public async Task InitializeAsync()
    {
        if (_sqlContainer is not null)
        {
            await _sqlContainer.StartAsync();
            _serverConnectionString = _sqlContainer.GetConnectionString();
        }
        else
        {
            _serverConnectionString = ExternalConnectionString!;
        }

        // A dedicated database per fixture run, so a shared external server can host parallel runs without
        // collisions and a re-run always starts clean.
        _testDatabaseName = $"ReportTest_{Guid.NewGuid():N}";

        var connectionString = new SqlConnectionStringBuilder(_serverConnectionString)
        {
            InitialCatalog = _testDatabaseName,
            ConnectTimeout = 60
        }.ConnectionString;

        await using (var masterConnection = new SqlConnection(_serverConnectionString))
        {
            await masterConnection.OpenAsync();
            await using var command = masterConnection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{_testDatabaseName}];";
            await command.ExecuteNonQueryAsync();
        }

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        builder.Services.AddDbContext<ReportDbContext>(options => options.UseSqlServer(connectionString));
        builder.Services.AddScoped<IReportEntryMappingOutcomeManager, ReportEntryMappingOutcomeManager>();
        builder.Services.AddScoped<IReportEntryManager, ReportEntryManager>();

        _host = builder.Build();
        await _host.StartAsync();
        ScopeFactory = _host.Services.GetRequiredService<IServiceScopeFactory>();

        using var scope = ScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        if (_sqlContainer is not null)
        {
            await _sqlContainer.DisposeAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(_serverConnectionString) || string.IsNullOrWhiteSpace(_testDatabaseName))
        {
            return;
        }

        try
        {
            await using var masterConnection = new SqlConnection(_serverConnectionString);
            await masterConnection.OpenAsync();
            await using var command = masterConnection.CreateCommand();
            command.CommandText =
                $"ALTER DATABASE [{_testDatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{_testDatabaseName}];";
            await command.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best effort. The next run uses a new database name.
        }
    }
}
