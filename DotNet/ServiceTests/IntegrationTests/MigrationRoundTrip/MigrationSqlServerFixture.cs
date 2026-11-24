using Link.LocalDev;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.MigrationRoundTrip;

/// <summary>
/// One SQL Server for the migration round-trip tests. Each test creates its own empty database and drops it
/// afterward, so every service's migrations run from nothing.
/// </summary>
public sealed class MigrationSqlServerFixture : IAsyncLifetime
{
    private static readonly string SqlServerImage = DevStackImages.SqlServer.Reference;

    private readonly MsSqlContainer? _sqlContainer;
    private string _serverConnectionString = "";

    public MigrationSqlServerFixture()
    {
        if (string.IsNullOrWhiteSpace(ExternalConnectionString))
        {
            _sqlContainer = new MsSqlBuilder(SqlServerImage).Build();
        }
    }

    // Points the fixture at an existing SQL Server instead of a Testcontainer, like the Report and
    // DataAcquisition SQL Server fixtures.
    private static string? ExternalConnectionString =>
        Environment.GetEnvironmentVariable("LINK_TESTS_SQL_CONNECTION_STRING");

    /// <summary>
    /// Starts the container, or uses the external server when one is configured.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_sqlContainer is null)
        {
            _serverConnectionString = ExternalConnectionString!;
            return;
        }

        await _sqlContainer.StartAsync();
        _serverConnectionString = _sqlContainer.GetConnectionString();
    }

    /// <summary>
    /// Stops and removes the container. Databases on an external server are dropped by each test.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_sqlContainer is null)
        {
            return;
        }

        await _sqlContainer.DisposeAsync();
    }

    /// <summary>
    /// Creates an empty database with a unique name and returns its connection string.
    /// </summary>
    public async Task<string> CreateDatabaseAsync(string namePrefix)
    {
        var databaseName = $"{namePrefix}_{Guid.NewGuid():N}";

        await using (var masterConnection = new SqlConnection(_serverConnectionString))
        {
            await masterConnection.OpenAsync();
            await using var command = masterConnection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{databaseName}];";
            await command.ExecuteNonQueryAsync();
        }

        return new SqlConnectionStringBuilder(_serverConnectionString)
        {
            InitialCatalog = databaseName,
            ConnectTimeout = 60
        }.ConnectionString;
    }

    /// <summary>
    /// Drops a database made by <see cref="CreateDatabaseAsync"/>. Best effort, since every run uses new names.
    /// </summary>
    public async Task DropDatabaseAsync(string connectionString)
    {
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        // The test's pooled connections would otherwise keep the database in use.
        using (var pooledConnection = new SqlConnection(connectionString))
        {
            SqlConnection.ClearPool(pooledConnection);
        }

        try
        {
            await using var masterConnection = new SqlConnection(_serverConnectionString);
            await masterConnection.OpenAsync();
            await using var command = masterConnection.CreateCommand();
            command.CommandText =
                $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{databaseName}];";
            await command.ExecuteNonQueryAsync();
        }
        catch (SqlException)
        {
            // Best effort. The next run uses a new database name.
        }
    }
}
