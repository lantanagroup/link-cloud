using System.Security.Claims;
using Claim = System.Security.Claims.Claim;
using ClaimsIdentity = System.Security.Claims.ClaimsIdentity;
using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;
using Link.Authorization.Infrastructure;
using Link.Authorization.Permissions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace UnitTests.Kafka;

[Trait("Category", "UnitTests")]
public class MigrationPermissionTests
{
    [Fact]
    public async Task MissingMigrateClaim_IsForbidden_BeforeAMissingMigrationIsNotFound()
    {
        var runtime = Runtime(anonymous: false);
        var stranger = new ClaimsPrincipal(new ClaimsIdentity("test"));
        var missing = Guid.NewGuid();

        var forbidden = await Assert.ThrowsAsync<KafkaOpsForbiddenException>(() =>
            runtime.GetAsync(stranger, missing, mutate: true, CancellationToken.None));
        Assert.Contains("CanMigrateKafkaTopics", forbidden.Message, StringComparison.Ordinal);

        var viewer = Principal(nameof(LinkSystemPermissions.CanViewInfrastructure));
        var notFound = await Assert.ThrowsAsync<KafkaOpsNotFoundException>(() =>
            runtime.GetAsync(viewer, missing, mutate: false, CancellationToken.None));
        Assert.Contains("not found", notFound.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MigrateClaim_LooksUpTheRecord_AndAMissingIdIsNotFound()
    {
        var runtime = Runtime(anonymous: false);
        var migrator = Principal(nameof(LinkSystemPermissions.CanMigrateKafkaTopics));
        await Assert.ThrowsAsync<KafkaOpsNotFoundException>(() =>
            runtime.GetAsync(migrator, Guid.NewGuid(), mutate: true, CancellationToken.None));
    }

    private static MigrationRuntime Runtime(bool anonymous)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:EnableAnonymousAccess"] = anonymous ? "true" : "false"
            })
            .Build();
        return new MigrationRuntime(
            configuration,
            new TestHost(),
            Options.Create(new KafkaOpsOptions { InfraProvider = "LocalCompose", AllowTopicMigration = true }),
            new InMemoryMigrationStore(),
            new InMemoryKafkaOpsLease(),
            new UnusedAdmin(),
            new UnusedWorkloads(),
            new UnusedInfra(),
            new MigrationHoldRegistry(),
            new UnusedCache(),
            NullLogger<MigrationRuntime>.Instance);
    }

    private static ClaimsPrincipal Principal(string permission) =>
        new(new ClaimsIdentity(
            [new Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, permission)],
            "test"));

    private sealed class TestHost : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class UnusedAdmin : IKafkaMigrationAdmin
    {
        public bool DedicatedConnection => true;
        public Task<bool> ClusterHealthyAsync(CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<MigrationTopicFacts?> DescribeAsync(string topic, CancellationToken cancellationToken) => Task.FromResult<MigrationTopicFacts?>(null);
        public Task<IReadOnlyList<TopicConfigRow>> DescribeConfigsAsync(string topic, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TopicConfigRow>>([]);
        public Task<IReadOnlyDictionary<string, string>> DescribeBrokerConfigAsync(int brokerId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        public Task<string> CreateTopicAsync(string name, int partitions, int replicationFactor, IReadOnlyDictionary<string, string> configs, bool validateOnly, CancellationToken cancellationToken) => Task.FromResult("");
        public Task DeleteTopicAsync(string name, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task GrowPartitionsAsync(string name, int to, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetRetentionAsync(string topic, long retentionMs, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task WriteOffsetsAsync(string group, string topic, IReadOnlyList<long> offsets, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<MigrationGroupFacts?> DescribeGroupAsync(string group, string topic, int partitions, CancellationToken cancellationToken) => Task.FromResult<MigrationGroupFacts?>(null);
        public Task ElectLeadersAsync(string topic, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> ListGroupsWhenHealthyAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<CopyOutcome> CopyBackupAsync(MigrationRecord record, CancellationToken cancellationToken) => Task.FromResult(new CopyOutcome());
        public Task<CopyOutcome> InspectBackupAsync(MigrationRecord record, CancellationToken cancellationToken) => Task.FromResult(new CopyOutcome());
        public Task AppendJournalAsync(MigrationRecord record, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<MigrationRecord>> ReadJournalAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<MigrationRecord>>([]);
        public Task<IReadOnlyList<string>> ListLinkMigTopicsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task EnsureJournalAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class UnusedWorkloads : IKafkaWorkloadControl
    {
        public bool ManualChecklist => false;
        public bool TryMap(string workload, out string target) { target = ""; return false; }
        public Task<int?> ReadReplicasAsync(string workload, CancellationToken cancellationToken) => Task.FromResult<int?>(null);
        public Task ScaleAsync(string workload, int replicas, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> TopicOperatorOwnsAsync(string topic, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class UnusedInfra : IKafkaInfraProvider
    {
        public string Name => "LocalCompose";
        public bool Enabled => true;
        public string Detail => "";
        public Task ScaleGroupAsync(string groupId, int replicas, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddBrokerAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RemoveBrokerAsync(int brokerId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ApplyReassignmentAsync(string reassignmentJson, string rebalanceName, bool refresh, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CancelReassignmentAsync(string rebalanceName, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ReleaseRebalanceAsync(string rebalanceName, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<ReassignmentListing> ListInFlightReassignmentsAsync(CancellationToken cancellationToken) => Task.FromResult(new ReassignmentListing { Known = true });
    }

    private sealed class UnusedCache : LantanaGroup.Link.Shared.Application.Interfaces.ICacheService
    {
        public Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T>(default!);
        public Task SetAsync<T>(string key, T value, TimeSpan expiration, LantanaGroup.Link.Shared.Application.Models.Configs.ExpirationType expirationType = LantanaGroup.Link.Shared.Application.Models.Configs.ExpirationType.Sliding, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
