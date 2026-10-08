using Task = System.Threading.Tasks.Task;
using Claim = System.Security.Claims.Claim;
using System.Security.Claims;
using Confluent.Kafka;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Link.Authorization.Infrastructure;
using Link.Authorization.Permissions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace UnitTests.Kafka;

[Trait("Category", "UnitTests")]
public class KafkaOpsServiceGuardTests
{
    [Fact]
    public void ExecutionPermission_MatchesTheChangeKind()
    {
        Assert.True(KafkaOpsExecution.Allows(true, false, KafkaChangeKind.PartitionIncrease));
        Assert.False(KafkaOpsExecution.Allows(false, true, KafkaChangeKind.PartitionIncrease));
        Assert.False(KafkaOpsExecution.Allows(false, false, KafkaChangeKind.PartitionIncrease));
        Assert.True(KafkaOpsExecution.Allows(false, true, KafkaChangeKind.ScaleReplicas));
        Assert.False(KafkaOpsExecution.Allows(true, false, KafkaChangeKind.ScaleReplicas));
        Assert.False(KafkaOpsExecution.Allows(false, false, KafkaChangeKind.ScaleReplicas));
    }

    [Fact]
    public void ExecuteEndpoint_ChecksThePermissionThatMatchesTheKind()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "DotNet", "Admin.BFF", "Presentation", "Endpoints", "KafkaOpsEndpoints.cs"));
        Assert.Contains("KafkaOpsExecution.Allows", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AuditProducer_DoesNotAutoCreateTheAuditTopic()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "DotNet", "Admin.BFF", "Program.cs"));
        var line = text.Split('\n').Single(candidate => candidate.Contains("AuditEventMessage", StringComparison.Ordinal));
        Assert.Contains("AllowAutoCreateTopics = false", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plan_RefusesWhenSubscribedGroupsCannotBeRead()
    {
        var (service, broker, _) = NewService(requireSecondApprover: true);
        broker.GroupError = new InvalidOperationException("one group at a time");

        var error = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.PlanAsync("ReadyToAcquire", 4, false, null, CancellationToken.None));

        Assert.Contains("could not be read", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plan_RefusesARetryTopicOnItsOwn()
    {
        var (service, _, _) = NewService(requireSecondApprover: true);

        var plan = await service.PlanAsync("ReadyToAcquire-Retry", 4, false, null, CancellationToken.None);

        Assert.False(plan.Accepted);
        Assert.Contains(plan.Errors, error => error.Contains("not increased", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Topics_MarkLagUnknownWhenTheGroupReadFails()
    {
        var (service, broker, _) = NewService(requireSecondApprover: true);
        broker.GroupError = new InvalidOperationException("one group at a time");

        var topics = await service.GetTopicsAsync(CancellationToken.None);

        Assert.Null(topics.Error);
        Assert.Contains("could not be read", topics.GroupsError, StringComparison.Ordinal);
        Assert.NotEmpty(topics.Topics);
        Assert.All(topics.Topics, row => Assert.False(row.LagKnown));
    }

    [Fact]
    public async Task Execute_LetsADifferentManagerRaisePartitions_AndKeepsTheRateLimit()
    {
        var (service, broker, _) = NewService(requireSecondApprover: true);
        var created = await service.CreateAsync(User("alice", ManageTopics), "ReadyToAcquire", 4, "raise the log topic", false, null, "ReadyToAcquire", null, CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.Pending, created.Status);

        await service.ApproveAsync(User("bob", ManageTopics), created.Id, CancellationToken.None);

        var scalingOnly = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ExecuteAsync(User("sam", ManageScaling), created.Id, CancellationToken.None));
        Assert.Contains("not allowed", scalingOnly.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(KafkaChangeStatus.Approved, (await service.GetAsync(created.Id, CancellationToken.None))!.Status);

        var self = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ExecuteAsync(User("alice", ManageTopics), created.Id, CancellationToken.None));
        Assert.Contains("cannot execute", self.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(KafkaChangeStatus.Approved, (await service.GetAsync(created.Id, CancellationToken.None))!.Status);

        var executed = await service.ExecuteAsync(User("carol", ManageTopics), created.Id, CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.Converging, executed.Status);
        Assert.NotNull(executed.PartitionsChangedUtc);
        Assert.Contains(("ReadyToAcquire", 4), broker.Increases);
        Assert.Contains(("ReadyToAcquire-Retry", 4), broker.Increases);
        Assert.Contains((KafkaTopicCatalog.ErrorName("ReadyToAcquire"), 4), broker.Increases);

        var again = await service.PlanAsync("ReadyToAcquire", 5, false, null, CancellationToken.None);
        Assert.False(again.Accepted);
        Assert.Contains(again.Errors, error => error.Contains("minutes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Execute_StoresFailedWhenTheGroupReadFailsAfterApproval()
    {
        var (service, broker, _) = NewService(requireSecondApprover: true);
        var created = await service.CreateAsync(User("alice", ManageTopics), "ReadyToAcquire", 4, "raise the log topic", false, null, "ReadyToAcquire", null, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageTopics), created.Id, CancellationToken.None);
        broker.GroupError = new InvalidOperationException("groups down");

        var error = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ExecuteAsync(User("carol", ManageTopics), created.Id, CancellationToken.None));

        var stored = await service.GetAsync(created.Id, CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.Failed, stored!.Status);
        Assert.Null(stored.PartitionsChangedUtc);
        Assert.Contains("could not be read", stored.Failure, StringComparison.Ordinal);
        Assert.Contains("could not be read", error.Message, StringComparison.Ordinal);
        Assert.Empty(broker.Increases);

        broker.GroupError = null;
        var again = await service.PlanAsync("ReadyToAcquire", 4, false, null, CancellationToken.None);
        Assert.True(again.Accepted);
        Assert.DoesNotContain(again.Errors, item => item.Contains("minutes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Capabilities_AreCachedUntilTheTtl()
    {
        var (service, broker, _) = NewService(requireSecondApprover: true);

        await service.GetCapabilitiesAsync(CancellationToken.None);
        await service.GetCapabilitiesAsync(CancellationToken.None);

        Assert.Equal(1, broker.CapabilityProbes);
    }

    [Fact]
    public async Task ScaleToZero_SettlesWhenTheGroupHasNoMembers()
    {
        var (service, broker, _) = NewService(requireSecondApprover: false);
        broker.Groups.Add(new GroupView
        {
            GroupId = "DataAcquisitionWorker",
            State = "Stable",
            Members = [new GroupMemberView { ClientId = "data-acquisition-host-c1" }]
        });

        var created = await service.CreateScaleAsync(User("pat", ManageScaling), "DataAcquisitionWorker", 0, "drain the worker", null, CancellationToken.None);
        var executed = await service.ExecuteAsync(User("pat", ManageScaling), created.Id, CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.Converging, executed.Status);

        broker.Groups.Clear();
        broker.Groups.Add(new GroupView { GroupId = "DataAcquisitionWorker", State = "Empty" });
        await service.TrackAsync(CancellationToken.None);

        var stored = await service.GetAsync(created.Id, CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.Done, stored!.Status);
        Assert.Contains("no members", stored.Progress, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_StoresFailedWhenTheProviderIsDisabledAfterTheRequestStarts()
    {
        var (service, _, infra) = NewService(requireSecondApprover: false);
        var created = await service.CreateScaleAsync(User("pat", ManageScaling), "DataAcquisitionWorker", 1, "add a worker", null, CancellationToken.None);
        infra.Enabled = false;

        await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ExecuteAsync(User("pat", ManageScaling), created.Id, CancellationToken.None));

        var stored = await service.GetAsync(created.Id, CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.Failed, stored!.Status);
        Assert.NotEqual(KafkaChangeStatus.Executing, stored.Status);
    }

    [Fact]
    public async Task Execute_StoresFailedWhenTheScaleIsRefused()
    {
        var (service, broker, infra) = NewService(requireSecondApprover: false);
        broker.Groups.Add(new GroupView
        {
            GroupId = "DataAcquisitionWorker",
            State = "Stable",
            Members = [new GroupMemberView { ClientId = "data-acquisition-host-c1" }]
        });
        var created = await service.CreateScaleAsync(User("pat", ManageScaling), "DataAcquisitionWorker", 0, "drain the worker", null, CancellationToken.None);
        infra.ThrowOnScale = true;

        await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ExecuteAsync(User("pat", ManageScaling), created.Id, CancellationToken.None));

        var stored = await service.GetAsync(created.Id, CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.Failed, stored!.Status);
        Assert.Contains("refused", stored.Failure, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Track_TimesOutWithoutRemovingTheBroker()
    {
        var (service, broker, infra) = NewService(requireSecondApprover: false, scaleTimeoutSeconds: 30);
        var created = await service.CreateScaleAsync(User("pat", ManageScaling), "DataAcquisitionWorker", 1, "add a worker", null, CancellationToken.None);
        await service.ExecuteAsync(User("pat", ManageScaling), created.Id, CancellationToken.None);
        var stored = (await service.GetAsync(created.Id, CancellationToken.None))!;
        stored.ExecutedUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
        broker.GroupError = new InvalidOperationException("groups down");

        await service.TrackAsync(CancellationToken.None);

        Assert.Equal(KafkaChangeStatus.TimedOut, stored.Status);
        Assert.Contains("last poll failed", stored.Failure, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, infra.RemoveCalls);
    }

    [Fact]
    public async Task Track_RetriesAPollFailureUntilTheTimeout()
    {
        var (service, broker, _) = NewService(requireSecondApprover: false, scaleTimeoutSeconds: 180);
        var created = await service.CreateScaleAsync(User("pat", ManageScaling), "DataAcquisitionWorker", 1, "add a worker", null, CancellationToken.None);
        await service.ExecuteAsync(User("pat", ManageScaling), created.Id, CancellationToken.None);
        var before = broker.GroupCalls;
        broker.GroupError = new InvalidOperationException("groups down");

        await service.TrackAsync(CancellationToken.None);
        var stored = (await service.GetAsync(created.Id, CancellationToken.None))!;
        Assert.Equal(KafkaChangeStatus.Converging, stored.Status);
        Assert.True(stored.NextPollUtc > DateTimeOffset.UtcNow.AddSeconds(-1));
        Assert.Contains("retried", stored.Progress, StringComparison.OrdinalIgnoreCase);
        var after = broker.GroupCalls;

        await service.TrackAsync(CancellationToken.None);
        Assert.Equal(after, broker.GroupCalls);
        Assert.True(after > before);
    }

    [Fact]
    public async Task Decommission_UsesProcessRoles_AndRefusesWhenRolesAreUnknown()
    {
        var (service, broker, _) = NewService(requireSecondApprover: true);
        broker.ControllerId = 2;
        broker.Eligible.Clear();
        broker.Eligible.Add(0);
        broker.RolesKnown = true;
        broker.Placements.Add(new PartitionPlacement
        {
            Topic = "ReadyToAcquire",
            Partition = 0,
            Leader = 1,
            Replicas = [1, 2, 3],
            Isr = [1, 2, 3]
        });

        var ordinary = await service.PlanDecommissionAsync(3, CancellationToken.None);
        Assert.True(ordinary.Accepted);
        var rotating = await service.PlanDecommissionAsync(2, CancellationToken.None);
        Assert.True(rotating.Accepted);
        var controller = await service.PlanDecommissionAsync(0, CancellationToken.None);
        Assert.False(controller.Accepted);
        Assert.Contains(controller.Errors, error => error.Contains("controller", StringComparison.OrdinalIgnoreCase));

        var created = await service.CreateDecommissionAsync(User("alice", ManageScaling), 3, "remove the spare broker", null, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), created.Id, CancellationToken.None);
        broker.RolesKnown = false;

        var blocked = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ExecuteAsync(User("carol", ManageScaling), created.Id, CancellationToken.None));
        Assert.Contains("could not be read", blocked.Message, StringComparison.Ordinal);
        var stored = await service.GetAsync(created.Id, CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.Failed, stored!.Status);

        var unknown = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.PlanDecommissionAsync(3, CancellationToken.None));
        Assert.Contains("could not be read", unknown.Message, StringComparison.Ordinal);
    }

    private const string ManageTopics = nameof(LinkSystemPermissions.CanManageKafkaTopics);
    private const string ManageScaling = nameof(LinkSystemPermissions.CanManageScaling);

    private static (KafkaOpsService Service, FakeBroker Broker, FlipInfra Infra) NewService(bool requireSecondApprover, int scaleTimeoutSeconds = 180)
    {
        var broker = new FakeBroker();
        var infra = new FlipInfra();
        var options = Options.Create(new KafkaOpsOptions
        {
            RequireSecondApprover = requireSecondApprover,
            MaxPartitionsPerTopic = 24,
            RateLimitMinutes = 30,
            ScaleTimeoutSeconds = scaleTimeoutSeconds,
            CacheSeconds = 8
        });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:EnableAnonymousAccess"] = "false" })
            .Build();
        var service = new KafkaOpsService(
            broker,
            new SameReferenceCache(),
            options,
            new TestHost(),
            NullLogger<KafkaOpsService>.Instance,
            configuration,
            Array.Empty<IProducer<string, AuditEventMessage>>(),
            infra);
        return (service, broker, infra);
    }

    private static ClaimsPrincipal User(string name, string permission)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, name),
                new Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, permission)
            ],
            "test",
            ClaimTypes.Name,
            ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "topics.txt")) && Directory.Exists(Path.Combine(dir.FullName, "DotNet")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("The repository root was not found from the test output directory.");
    }

    private sealed class TestHost : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class SameReferenceCache : ICacheService
    {
        private readonly Dictionary<string, object> _store = new(StringComparer.Ordinal);

        public Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (_store.TryGetValue(key, out var value) && value is T typed)
                return Task.FromResult(typed);
            return Task.FromResult(default(T)!);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan expiration, ExpirationType expirationType = ExpirationType.Sliding, CancellationToken cancellationToken = default)
        {
            _store[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FlipInfra : IKafkaInfraProvider
    {
        public bool Enabled { get; set; } = true;
        public bool ThrowOnScale { get; set; }
        public int RemoveCalls { get; private set; }
        public string Name => "Test";
        public string Detail => "Infrastructure is disabled for this test.";

        public Task ScaleGroupAsync(string groupId, int replicas, CancellationToken cancellationToken)
        {
            if (ThrowOnScale)
                throw new KafkaOpsRejectedException("The scale was refused.");
            return Task.CompletedTask;
        }

        public Task AddBrokerAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveBrokerAsync(int brokerId, CancellationToken cancellationToken)
        {
            RemoveCalls++;
            return Task.CompletedTask;
        }

        public Task ApplyReassignmentAsync(string reassignmentJson, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CancelReassignmentAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeBroker : IKafkaBrokerGateway
    {
        public Exception? GroupError { get; set; }
        public int GroupCalls { get; private set; }
        public int CapabilityProbes { get; private set; }
        public List<GroupView> Groups { get; } = [];
        public List<(string Topic, int Count)> Increases { get; } = [];
        public List<PartitionPlacement> Placements { get; } = [];
        public int ControllerId { get; set; } = 1;
        public bool RolesKnown { get; set; } = true;
        public List<int> Eligible { get; } = [0, 1, 2];
        private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<TopicWatermark>> DescribeTopicsAsync(IReadOnlyList<string> topics, bool probeAlter, CancellationToken cancellationToken)
        {
            if (probeAlter)
                CapabilityProbes++;
            IReadOnlyList<TopicWatermark> rows = topics.Select(name => new TopicWatermark
            {
                Topic = name,
                Partitions = _counts.TryGetValue(name, out var count) ? count : 3,
                ReplicationFactor = 3,
                HighWatermarks = [0],
                CanAlterPartitions = true
            }).ToList();
            return Task.FromResult(rows);
        }

        public Task<IReadOnlyList<GroupView>> DescribeGroupsAsync(bool includeTestGroups, int expectedConfigVersion, CancellationToken cancellationToken)
        {
            GroupCalls++;
            if (GroupError is not null)
                throw GroupError;
            return Task.FromResult<IReadOnlyList<GroupView>>(Groups.ToList());
        }

        public Task IncreasePartitionsAsync(string topic, int newCount, CancellationToken cancellationToken)
        {
            Increases.Add((topic, newCount));
            _counts[topic] = newCount;
            return Task.CompletedTask;
        }

        public Task<ClusterSnapshot> DescribeClusterAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new ClusterSnapshot
            {
                BrokerCount = 4,
                ControllerId = ControllerId,
                ControllerRolesKnown = RolesKnown,
                ControllerEligibleIds = Eligible.ToList(),
                Brokers =
                [
                    new BrokerSnapshot { Id = 0, State = "up" },
                    new BrokerSnapshot { Id = 1, State = "up" },
                    new BrokerSnapshot { Id = 2, State = "up" },
                    new BrokerSnapshot { Id = 3, State = "up" }
                ],
                Placements = Placements.ToList()
            });
        }
    }
}
