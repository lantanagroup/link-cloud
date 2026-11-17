using System.Data;
using System.Diagnostics;
using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Normalization;

/// <summary>
/// The enabler only changes the Normalization database it is given. These tests use a
/// side database on the shared SQL Server container so the fixture database stays as it is.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class ReadCommittedSnapshotEnablerTests
{
    private readonly NormalizationSqlServerFixture _fixture;

    public ReadCommittedSnapshotEnablerTests(NormalizationSqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AlreadyOn_DoesNothingAndLeavesOtherSessionsAlone()
    {
        var databaseName = SafeName("rcsion");
        var admin = AdminConnection();
        await CreateDatabaseAsync(admin, databaseName);
        try
        {
            await SetSnapshotReadsAsync(admin, databaseName, enabled: true);
            var side = SideConnection(databaseName);
            await using var blocker = new SqlConnection(side);
            await blocker.OpenAsync();
            await using var transaction = (SqlTransaction)await blocker.BeginTransactionAsync();
            await using (var probe = blocker.CreateCommand())
            {
                probe.Transaction = transaction;
                probe.CommandText = "SELECT 1";
                Assert.Equal(1, await probe.ExecuteScalarAsync());
            }

            var logger = new ListLogger();
            var result = await EnsureAsync(side, logger: logger);

            Assert.Equal(ReadCommittedSnapshotResult.AlreadyEnabled, result);
            Assert.True(await SnapshotReadsEnabledAsync(admin, databaseName));
            Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
            await using (var probe = blocker.CreateCommand())
            {
                probe.Transaction = transaction;
                probe.CommandText = "SELECT 1";
                Assert.Equal(1, await probe.ExecuteScalarAsync());
            }
        }
        finally
        {
            await DropDatabaseAsync(admin, databaseName);
        }
    }

    [Fact]
    public async Task WhenOffAndIdle_EnablesSnapshotReads()
    {
        var databaseName = SafeName("rcsioff");
        var admin = AdminConnection();
        await CreateDatabaseAsync(admin, databaseName);
        try
        {
            Assert.False(await SnapshotReadsEnabledAsync(admin, databaseName));

            var logger = new ListLogger();
            var result = await EnsureAsync(SideConnection(databaseName), logger: logger);

            Assert.Equal(ReadCommittedSnapshotResult.Enabled, result);
            Assert.True(await SnapshotReadsEnabledAsync(admin, databaseName));
            Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
        }
        finally
        {
            await DropDatabaseAsync(admin, databaseName);
        }
    }

    [Fact]
    public async Task WhenOffAndAnotherSessionIsConnected_ContinuesWithoutEnabling()
    {
        var databaseName = SafeName("rcsiblk");
        var admin = AdminConnection();
        await CreateDatabaseAsync(admin, databaseName);
        try
        {
            await using var blocker = new SqlConnection(SideConnection(databaseName));
            await blocker.OpenAsync();
            await using var hold = blocker.CreateCommand();
            hold.CommandText = "SELECT 1";
            Assert.Equal(1, await hold.ExecuteScalarAsync());

            var logger = new ListLogger();
            var result = await EnsureAsync(
                SideConnection(databaseName),
                FastRetry(maxAttempts: 2),
                logger);

            Assert.Equal(ReadCommittedSnapshotResult.SkippedBlocked, result);
            Assert.False(await SnapshotReadsEnabledAsync(admin, databaseName));
            Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information && entry.Message.Contains("Skipping this attempt", StringComparison.Ordinal));
            await AssertSessionStillUsableAsync(blocker);
        }
        finally
        {
            await DropDatabaseAsync(admin, databaseName);
        }
    }

    [Fact]
    public async Task WhenOtherSessionsRemain_ExhaustsRetriesAndContinues()
    {
        var databaseName = SafeName("rcsiretry");
        var admin = AdminConnection();
        await CreateDatabaseAsync(admin, databaseName);
        try
        {
            await using var blocker = new SqlConnection(SideConnection(databaseName));
            await blocker.OpenAsync();

            var logger = new ListLogger();
            var result = await EnsureAsync(
                SideConnection(databaseName),
                FastRetry(maxAttempts: 3),
                logger);

            Assert.Equal(ReadCommittedSnapshotResult.SkippedBlocked, result);
            Assert.False(await SnapshotReadsEnabledAsync(admin, databaseName));
            Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Startup continues", StringComparison.Ordinal));
            await AssertSessionStillUsableAsync(blocker);
        }
        finally
        {
            await DropDatabaseAsync(admin, databaseName);
        }
    }

    [Fact]
    public async Task WithoutAlterPermission_ContinuesWithoutEnabling()
    {
        var databaseName = SafeName("rcsiperm");
        var login = SafeName("rcsiuser");
        const string password = "Rcsi_Test_Passw0rd!";
        var admin = AdminConnection();
        await CreateDatabaseAsync(admin, databaseName);
        try
        {
            await CreateLoginAsync(admin, databaseName, login, password);
            var limited = new SqlConnectionStringBuilder(SideConnection(databaseName))
            {
                UserID = login,
                Password = password
            }.ConnectionString;

            var started = Stopwatch.StartNew();
            var result = await EnsureAsync(limited, new ReadCommittedSnapshotRetry
            {
                MaxAttempts = 3,
                Delay = TimeSpan.FromSeconds(5),
                Jitter = TimeSpan.Zero,
                AlterTimeout = TimeSpan.FromSeconds(1)
            });
            started.Stop();

            Assert.Equal(ReadCommittedSnapshotResult.SkippedPermission, result);
            Assert.False(await SnapshotReadsEnabledAsync(admin, databaseName));
            Assert.True(started.Elapsed < TimeSpan.FromSeconds(6), $"Permission denial retried and took {started.Elapsed}.");
        }
        finally
        {
            await DropDatabaseAsync(admin, databaseName);
            await DropLoginAsync(admin, login);
        }
    }

    private static ReadCommittedSnapshotRetry FastRetry(int maxAttempts = 3)
    {
        return new ReadCommittedSnapshotRetry
        {
            MaxAttempts = maxAttempts,
            Delay = TimeSpan.FromMilliseconds(30),
            Jitter = TimeSpan.Zero,
            AlterTimeout = TimeSpan.FromSeconds(1)
        };
    }

    private async Task<ReadCommittedSnapshotResult> EnsureAsync(
        string connectionString,
        ReadCommittedSnapshotRetry? retry = null,
        ILogger? logger = null)
    {
        var options = new DbContextOptionsBuilder<NormalizationDbContext>().UseSqlServer(connectionString).Options;
        await using var context = new NormalizationDbContext(options);
        return await ReadCommittedSnapshotEnabler.EnsureAsync(
            context,
            logger ?? NullLogger.Instance,
            retry ?? FastRetry(),
            CancellationToken.None);
    }

    private static async Task AssertSessionStillUsableAsync(SqlConnection blocker)
    {
        Assert.Equal(ConnectionState.Open, blocker.State);
        await using var probe = blocker.CreateCommand();
        probe.CommandText = "SELECT 1";
        Assert.Equal(1, await probe.ExecuteScalarAsync());
    }

    private sealed class ListLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }

    private string AdminConnection()
    {
        var builder = new SqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            InitialCatalog = "master"
        };
        return builder.ConnectionString;
    }

    private string SideConnection(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            InitialCatalog = databaseName
        };
        return builder.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string adminConnection, string databaseName)
    {
        await using var connection = new SqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{databaseName}') IS NULL CREATE DATABASE [{databaseName}];";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SetSnapshotReadsAsync(string adminConnection, string databaseName, bool enabled)
    {
        var onOff = enabled ? "ON" : "OFF";
        await using var connection = new SqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{databaseName}] SET READ_COMMITTED_SNAPSHOT {onOff} WITH ROLLBACK IMMEDIATE;";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> SnapshotReadsEnabledAsync(string adminConnection, string databaseName)
    {
        await using var connection = new SqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = @name";
        command.Parameters.Add(new SqlParameter("@name", databaseName));
        var raw = await command.ExecuteScalarAsync();
        return raw is bool value ? value : Convert.ToInt32(raw) != 0;
    }

    private static async Task CreateLoginAsync(string adminConnection, string databaseName, string login, string password)
    {
        await using var connection = new SqlConnection(adminConnection);
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"CREATE LOGIN [{login}] WITH PASSWORD = N'{password}', CHECK_POLICY = OFF;";
            await command.ExecuteNonQueryAsync();
        }

        await using var side = new SqlConnection(new SqlConnectionStringBuilder(adminConnection) { InitialCatalog = databaseName }.ConnectionString);
        await side.OpenAsync();
        await using var user = side.CreateCommand();
        user.CommandText = $"CREATE USER [{login}] FOR LOGIN [{login}];";
        await user.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string adminConnection, string databaseName)
    {
        await using var connection = new SqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropLoginAsync(string adminConnection, string login)
    {
        await using var connection = new SqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF SUSER_ID(N'{login}') IS NOT NULL DROP LOGIN [{login}];
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static string SafeName(string prefix)
    {
        return prefix + Guid.NewGuid().ToString("N")[..8];
    }
}
