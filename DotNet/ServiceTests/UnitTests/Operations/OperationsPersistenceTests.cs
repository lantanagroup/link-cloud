using System.Text.Json;
using LantanaGroup.Link.Operations.Application.KafkaOps;
using LantanaGroup.Link.Operations.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Operations;

/// <summary>
/// The Operations service keeps change requests, the executor lease and migrations in SQL so that
/// several replicas can run side by side. These run the real stores against SQLite.
/// </summary>
[Trait("Category", "UnitTests")]
public sealed class OperationsPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestFactory _factory;

    public OperationsPersistenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _factory = new TestFactory(_connection);
        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task ChangeRequests_RoundTrip_AndAdvanceTheVersion()
    {
        var store = new SqlChangeRequestStore(_factory, "Test", 30);
        var record = NewRecord(KafkaChangeStatus.Pending);

        await store.SaveAsync(record, CancellationToken.None);
        Assert.Equal(1, record.StoreVersion);

        var read = await store.GetAsync(record.Id, CancellationToken.None);
        Assert.NotNull(read);
        Assert.Equal(record.Topic, read!.Topic);
        Assert.Equal(1, read.StoreVersion);

        read.Status = KafkaChangeStatus.Approved;
        await store.SaveAsync(read, CancellationToken.None);
        Assert.Equal(2, read.StoreVersion);
        Assert.Equal(KafkaChangeStatus.Approved, (await store.GetAsync(record.Id, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task ChangeRequests_TwoReplicasSavingTheSameVersion_OnlyOneWins()
    {
        var store = new SqlChangeRequestStore(_factory, "Test", 30);
        var record = NewRecord(KafkaChangeStatus.Approved);
        await store.SaveAsync(record, CancellationToken.None);

        var first = await store.GetAsync(record.Id, CancellationToken.None);
        var second = await store.GetAsync(record.Id, CancellationToken.None);
        first!.Status = KafkaChangeStatus.Executing;
        second!.Status = KafkaChangeStatus.Executing;

        await store.SaveAsync(first, CancellationToken.None);
        var conflict = await Assert.ThrowsAsync<ChangeRequestConflictException>(() => store.SaveAsync(second, CancellationToken.None));
        Assert.Equal(ChangeRequestConflictException.Detail, conflict.Message);
    }

    [Fact]
    public async Task ChangeRequests_ASecondInsertOfTheSameId_IsAConflict()
    {
        var store = new SqlChangeRequestStore(_factory, "Test", 30);
        var record = NewRecord(KafkaChangeStatus.Pending);
        await store.SaveAsync(record, CancellationToken.None);
        var copy = JsonSerializer.Deserialize<ChangeRequestRecord>(JsonSerializer.Serialize(record))!;
        copy.StoreVersion = 0;

        await Assert.ThrowsAsync<ChangeRequestConflictException>(() => store.SaveAsync(copy, CancellationToken.None));
    }

    [Fact]
    public async Task ChangeRequests_ListKeepsOpenRequests_AndDropsOldClosedOnes()
    {
        var store = new SqlChangeRequestStore(_factory, "Test", 30);
        var open = NewRecord(KafkaChangeStatus.Converging, DateTimeOffset.UtcNow.AddDays(-90));
        var recent = NewRecord(KafkaChangeStatus.Done, DateTimeOffset.UtcNow.AddDays(-1));
        var old = NewRecord(KafkaChangeStatus.Done, DateTimeOffset.UtcNow.AddDays(-90));
        var other = NewRecord(KafkaChangeStatus.Pending);
        foreach (var item in new[] { open, recent, old })
            await store.SaveAsync(item, CancellationToken.None);
        await new SqlChangeRequestStore(_factory, "Other", 30).SaveAsync(other, CancellationToken.None);

        var listed = (await store.ListAsync(CancellationToken.None)).Select(item => item.Id).ToList();

        Assert.Equal([open.Id, recent.Id], listed);
    }

    [Fact]
    public async Task Lease_IsHeldByOneReplicaAtATime_AndTheFenceRisesOnEveryAcquire()
    {
        var a = new SqlExecutorLease(_factory, "Test", owner: "replica-a");
        var b = new SqlExecutorLease(_factory, "Test", owner: "replica-b");

        long firstFence;
        await using (var hold = await a.AcquireAsync(CancellationToken.None))
        {
            firstFence = hold.Fence;
            await Assert.ThrowsAsync<LeaseHeldException>(() => b.AcquireAsync(CancellationToken.None));
            // Not re-entrant inside one replica either: tracking and a migration step never overlap.
            await Assert.ThrowsAsync<LeaseHeldException>(() => a.AcquireAsync(CancellationToken.None));
        }

        await using var next = await b.AcquireAsync(CancellationToken.None);
        Assert.True(next.Fence > firstFence);
    }

    [Fact]
    public async Task Lease_LeftByAStoppedReplica_IsTakenOverAfterItExpires()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var stopped = new SqlExecutorLease(_factory, "Test", TimeSpan.FromSeconds(60), "replica-a", clock);
        var survivor = new SqlExecutorLease(_factory, "Test", TimeSpan.FromSeconds(60), "replica-b", clock);

        var abandoned = await stopped.AcquireAsync(CancellationToken.None);
        await Assert.ThrowsAsync<LeaseHeldException>(() => survivor.AcquireAsync(CancellationToken.None));

        clock.Advance(TimeSpan.FromSeconds(61));
        await using var taken = await survivor.AcquireAsync(CancellationToken.None);
        Assert.True(taken.Fence > abandoned.Fence);

        // The old holder releasing late does not clear the new holder's lease.
        await abandoned.DisposeAsync();
        await Assert.ThrowsAsync<LeaseHeldException>(() => new SqlExecutorLease(_factory, "Test", TimeSpan.FromSeconds(60), "replica-c", clock).AcquireAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Leases_AreSeparatePerEnvironment()
    {
        await using var test = await new SqlExecutorLease(_factory, "Test", owner: "a").AcquireAsync(CancellationToken.None);
        await using var other = await new SqlExecutorLease(_factory, "Other", owner: "b").AcquireAsync(CancellationToken.None);
        Assert.True(test.Fence > 0 && other.Fence > 0);
    }

    [Fact]
    public async Task Migrations_ASaveWithAnOlderFence_IsRefused()
    {
        var store = new SqlMigrationStore(_factory, "Test");
        var record = new MigrationRecord { Id = Guid.NewGuid(), Topic = "ResourcesAcquired", TargetPartitions = 6 };

        await store.SaveAsync(record, 5, CancellationToken.None);
        await store.SaveAsync(record, 5, CancellationToken.None);
        await Assert.ThrowsAsync<StaleFenceException>(() => store.SaveAsync(record, 4, CancellationToken.None));
        await store.SaveAsync(record, 6, CancellationToken.None);

        var read = await store.GetAsync(record.Id, CancellationToken.None);
        Assert.Equal(6, read!.Fence);
        Assert.Single(await store.ListAsync(CancellationToken.None));
        Assert.Empty(await new SqlMigrationStore(_factory, "Other").ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InMemoryStore_RefusesAStaleSaveLikeTheSqlStore()
    {
        var store = new InMemoryChangeRequestStore();
        var record = NewRecord(KafkaChangeStatus.Approved);
        await store.SaveAsync(record, CancellationToken.None);
        var first = await store.GetAsync(record.Id, CancellationToken.None);
        var second = await store.GetAsync(record.Id, CancellationToken.None);

        await store.SaveAsync(first!, CancellationToken.None);
        await Assert.ThrowsAsync<ChangeRequestConflictException>(() => store.SaveAsync(second!, CancellationToken.None));
    }

    private static ChangeRequestRecord NewRecord(KafkaChangeStatus status, DateTimeOffset? created = null) => new()
    {
        Id = Guid.NewGuid(),
        Kind = KafkaChangeKind.PartitionIncrease,
        Environment = "Test",
        Topic = "ResourcesAcquired",
        Family = "ResourcesAcquired",
        RequestedPartitions = 6,
        Status = status,
        CreatedUtc = created ?? DateTimeOffset.UtcNow
    };

    private sealed class TestFactory : IDbContextFactory<OperationsDbContext>
    {
        private readonly DbContextOptions<OperationsDbContext> _options;

        public TestFactory(SqliteConnection connection)
        {
            _options = new DbContextOptionsBuilder<OperationsDbContext>().UseSqlite(connection).Options;
        }

        public OperationsDbContext CreateDbContext() => new(_options);
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now;

        public ManualClock(DateTimeOffset now) => _now = now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
