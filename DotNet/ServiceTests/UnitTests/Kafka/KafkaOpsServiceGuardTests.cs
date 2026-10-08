using System.Text.Json;
using Task = System.Threading.Tasks.Task;
using Claim = System.Security.Claims.Claim;
using Moq;
using System.Security.Claims;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Link.Authorization.Infrastructure;
using Link.Authorization.Permissions;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints;
using Microsoft.AspNetCore.Http;
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
        Assert.True(KafkaOpsExecution.Allows(true, false, KafkaChangeKind.CompleteTopicFamily));
        Assert.False(KafkaOpsExecution.Allows(false, true, KafkaChangeKind.CompleteTopicFamily));
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

        var scalingOnly = await Assert.ThrowsAsync<KafkaOpsForbiddenException>(() =>
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
    public async Task Execute_StartsTheRateLimitWhenTheMainTopicChangedAndASiblingFailed()
    {
        var (service, broker, _) = NewService(requireSecondApprover: true);
        var created = await service.CreateAsync(User("alice", ManageTopics), "ReadyToAcquire", 4, "raise the log topic", false, null, "ReadyToAcquire", null, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageTopics), created.Id, CancellationToken.None);
        broker.FailIncreaseTopic = "ReadyToAcquire-Retry";

        var error = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ExecuteAsync(User("carol", ManageTopics), created.Id, CancellationToken.None));

        var stored = await service.GetAsync(created.Id, CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.Failed, stored!.Status);
        Assert.NotNull(stored.PartitionsChangedUtc);
        Assert.Contains("failed", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(("ReadyToAcquire", 4), broker.Increases);
        Assert.DoesNotContain(broker.Increases, item => item.Topic == "ReadyToAcquire-Retry");

        broker.FailIncreaseTopic = null;
        var again = await service.PlanAsync("ReadyToAcquire", 5, false, null, CancellationToken.None);
        Assert.False(again.Accepted);
        Assert.Contains(again.Errors, item => item.Contains("minutes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ApproveRejectAndCancel_WithTheWrongPermission_Return403()
    {
        var (service, _, _) = NewService(requireSecondApprover: true);
        var created = await service.CreateAsync(User("alice", ManageTopics), "ReadyToAcquire", 4, "raise the log topic", false, null, "ReadyToAcquire", null, CancellationToken.None);
        var endpoints = new KafkaOpsEndpoints(service, NullLogger<KafkaOpsEndpoints>.Instance);

        Assert.Equal(StatusCodes.Status403Forbidden, await StatusAsync(endpoints, "Approve", User("sam", ManageScaling), created.Id, null));
        Assert.Equal(StatusCodes.Status403Forbidden, await StatusAsync(endpoints, "Reject", User("sam", ManageScaling), created.Id, new RejectBody { Reason = "no" }));
        Assert.Equal(StatusCodes.Status403Forbidden, await StatusAsync(endpoints, "Cancel", User("sam", ManageScaling), created.Id, null));
        Assert.Equal(KafkaChangeStatus.Pending, (await service.GetAsync(created.Id, CancellationToken.None))!.Status);
        Assert.Equal(StatusCodes.Status400BadRequest, await StatusAsync(endpoints, "Approve", User("alice", ManageTopics), created.Id, null));
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

    [Fact]
    public async Task AdminClient_RebuildsAfterACoordinatorError()
    {
        var created = 0;
        var broken = new Mock<IAdminClient>();
        broken.Setup(client => client.DescribeConsumerGroupsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DescribeConsumerGroupsOptions>()))
            .ThrowsAsync(new KafkaException(ErrorCode.NotCoordinatorForGroup));
        var healthy = new Mock<IAdminClient>();
        healthy.Setup(client => client.DescribeConsumerGroupsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DescribeConsumerGroupsOptions>()))
            .ReturnsAsync(new DescribeConsumerGroupsResult());
        var factory = new Mock<IKafkaAdminClientFactory>();
        factory.Setup(item => item.Create(It.IsAny<AdminClientConfig>()))
            .Returns(() => ++created == 1 ? broken.Object : healthy.Object);

        var gateway = new KafkaBrokerGateway(new KafkaConnection { BootstrapServers = ["localhost:9092"] }, factory.Object);
        await Assert.ThrowsAsync<KafkaException>(() => gateway.DescribeGroupsAsync(false, 1, CancellationToken.None));
        Assert.Equal(2, created);
        broken.Verify(client => client.Dispose(), Times.Once);

        var groups = await gateway.DescribeGroupsAsync(false, 1, CancellationToken.None);
        Assert.Equal(2, created);
        Assert.NotEmpty(groups);
        Assert.All(groups, group => Assert.Empty(group.Members));
    }

    [Fact]
    public async Task AdminClient_RebuildsAfterRepeatedFailures()
    {
        var created = 0;
        var client = new Mock<IAdminClient>();
        client.Setup(item => item.DescribeConsumerGroupsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DescribeConsumerGroupsOptions>()))
            .ThrowsAsync(new InvalidOperationException("groups down"));
        var factory = new Mock<IKafkaAdminClientFactory>();
        factory.Setup(item => item.Create(It.IsAny<AdminClientConfig>()))
            .Returns(() =>
            {
                created++;
                return client.Object;
            });
        var gateway = new KafkaBrokerGateway(new KafkaConnection { BootstrapServers = ["localhost:9092"] }, factory.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.DescribeGroupsAsync(false, 1, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.DescribeGroupsAsync(false, 1, CancellationToken.None));
        Assert.Equal(1, created);

        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.DescribeGroupsAsync(false, 1, CancellationToken.None));
        Assert.Equal(2, created);
        client.Verify(item => item.Dispose(), Times.Once);

        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.DescribeGroupsAsync(false, 1, CancellationToken.None));
        Assert.Equal(2, created);
    }

    [Fact]
    public async Task PartialGroupError_RebuildsWithoutListingGroups()
    {
        var created = 0;
        var broken = new Mock<IAdminClient>();
        broken.Setup(client => client.DescribeConsumerGroupsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DescribeConsumerGroupsOptions>()))
            .ReturnsAsync(new DescribeConsumerGroupsResult
            {
                ConsumerGroupDescriptions =
                [
                    new ConsumerGroupDescription { GroupId = "Report", Error = new Error(ErrorCode.NotCoordinatorForGroup) }
                ]
            });
        var healthy = new Mock<IAdminClient>();
        healthy.Setup(client => client.DescribeConsumerGroupsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DescribeConsumerGroupsOptions>()))
            .ReturnsAsync(new DescribeConsumerGroupsResult());
        var factory = new Mock<IKafkaAdminClientFactory>();
        factory.Setup(item => item.Create(It.IsAny<AdminClientConfig>()))
            .Returns(() => ++created == 1 ? broken.Object : healthy.Object);
        var gateway = new KafkaBrokerGateway(new KafkaConnection { BootstrapServers = ["localhost:9092"] }, factory.Object);

        await Assert.ThrowsAsync<KafkaException>(() => gateway.DescribeGroupsAsync(false, 1, CancellationToken.None));
        Assert.Equal(2, created);
        broken.Verify(client => client.Dispose(), Times.Once);
        broken.Verify(client => client.ListConsumerGroupsAsync(It.IsAny<ListConsumerGroupsOptions>()), Times.Never);
    }

    [Fact]
    public async Task MissingCatalogGroups_RenderWithNoMembers()
    {
        var admin = new Mock<IAdminClient>();
        admin.Setup(client => client.DescribeConsumerGroupsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DescribeConsumerGroupsOptions>()))
            .ReturnsAsync(new DescribeConsumerGroupsResult
            {
                ConsumerGroupDescriptions =
                [
                    new ConsumerGroupDescription { GroupId = "Report", Error = new Error(ErrorCode.GroupIdNotFound) }
                ]
            });
        var factory = new Mock<IKafkaAdminClientFactory>();
        factory.Setup(item => item.Create(It.IsAny<AdminClientConfig>())).Returns(admin.Object);
        var gateway = new KafkaBrokerGateway(new KafkaConnection { BootstrapServers = ["localhost:9092"] }, factory.Object);

        var groups = await gateway.DescribeGroupsAsync(false, 1, CancellationToken.None);

        Assert.Contains(groups, group => group.GroupId == "Report" && group.Members.Count == 0 && group.State == "Empty");
        Assert.All(groups, group => Assert.Empty(group.Members));
        Assert.Equal(1, factory.Invocations.Count(invocation => invocation.Method.Name == "Create"));
    }

    [Fact]
    public async Task PartitionLeaderError_RebuildsTheClient()
    {
        var created = 0;
        var broken = GroupAdmin(new Error(ErrorCode.LeaderNotAvailable));
        var healthy = GroupAdmin(new Error(ErrorCode.NoError));
        var factory = new Mock<IKafkaAdminClientFactory>();
        factory.Setup(item => item.Create(It.IsAny<AdminClientConfig>()))
            .Returns(() => ++created == 1 ? broken.Object : healthy.Object);
        var gateway = new KafkaBrokerGateway(new KafkaConnection { BootstrapServers = ["localhost:9092"] }, factory.Object);

        await Assert.ThrowsAsync<KafkaException>(() => gateway.DescribeGroupsAsync(false, 1, CancellationToken.None));
        Assert.Equal(2, created);
        broken.Verify(client => client.Dispose(), Times.Once);
    }

    [Fact]
    public async Task FailureCounter_IsNotClearedByAnotherOperation()
    {
        var created = 0;
        var client = new Mock<IAdminClient>();
        client.Setup(item => item.DescribeConsumerGroupsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DescribeConsumerGroupsOptions>()))
            .ThrowsAsync(new InvalidOperationException("groups down"));
        var partition = new PartitionMetadata(0, 1, [1], [1], new Error(ErrorCode.NoError));
        client.Setup(item => item.GetMetadata(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(new Metadata([], [new TopicMetadata("ReadyToAcquire", [partition, partition, partition, partition], new Error(ErrorCode.NoError))], -1, ""));
        var factory = new Mock<IKafkaAdminClientFactory>();
        factory.Setup(item => item.Create(It.IsAny<AdminClientConfig>()))
            .Returns(() =>
            {
                created++;
                return client.Object;
            });
        var gateway = new KafkaBrokerGateway(new KafkaConnection { BootstrapServers = ["localhost:9092"] }, factory.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.DescribeGroupsAsync(false, 1, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.DescribeGroupsAsync(false, 1, CancellationToken.None));
        await gateway.IncreasePartitionsAsync("ReadyToAcquire", 4, CancellationToken.None);
        Assert.Equal(1, created);

        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.DescribeGroupsAsync(false, 1, CancellationToken.None));
        Assert.Equal(2, created);
    }

    [Fact]
    public async Task AdminCalls_DoNotOverlap()
    {
        var current = 0;
        var peak = 0;
        var admin = new Mock<IAdminClient>();
        admin.Setup(client => client.DescribeConsumerGroupsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DescribeConsumerGroupsOptions>()))
            .Returns(async () =>
            {
                var now = Interlocked.Increment(ref current);
                int snapshot;
                while ((snapshot = Volatile.Read(ref peak)) < now
                    && Interlocked.CompareExchange(ref peak, now, snapshot) != snapshot)
                {
                }

                await Task.Delay(60);
                Interlocked.Decrement(ref current);
                return new DescribeConsumerGroupsResult();
            });
        var factory = new Mock<IKafkaAdminClientFactory>();
        factory.Setup(item => item.Create(It.IsAny<AdminClientConfig>())).Returns(admin.Object);
        var gateway = new KafkaBrokerGateway(new KafkaConnection { BootstrapServers = ["localhost:9092"] }, factory.Object);

        await Task.WhenAll(
            gateway.DescribeGroupsAsync(false, 1, CancellationToken.None),
            gateway.DescribeGroupsAsync(false, 1, CancellationToken.None));

        Assert.Equal(1, peak);
    }

    [Fact]
    public void Console_DoesNotListConsumerGroups()
    {
        var root = Path.Combine(RepoRoot(), "DotNet", "Admin.BFF");
        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (!text.Contains("ListConsumerGroupsAsync", StringComparison.Ordinal))
                continue;
            Assert.EndsWith(Path.Combine("Migration", "KafkaMigrationAdmin.cs"), file, StringComparison.OrdinalIgnoreCase);
            var gate = text.IndexOf("if (!await ClusterHealthyAsync", StringComparison.Ordinal);
            var call = text.IndexOf("ListConsumerGroupsAsync", StringComparison.Ordinal);
            Assert.True(gate >= 0 && call > gate, "ListConsumerGroupsAsync must follow the healthy-cluster gate.");
        }
    }

    [Fact]
    public async Task ReadOnly_RefusesCreateApproveRejectAndCancelWith403()
    {
        var (service, _, _) = NewService(requireSecondApprover: true, production: true);
        var endpoints = new KafkaOpsEndpoints(service, NullLogger<KafkaOpsEndpoints>.Instance);
        var user = Operator();
        var http = new DefaultHttpContext();
        var id = Guid.NewGuid();
        var change = new ChangeRequestBody { Topic = "ReadyToAcquire", Partitions = 4, Reason = "raise it", Confirmation = "ReadyToAcquire" };
        var reason = new ReasonBody { Reason = "because" };

        Assert.Equal(StatusCodes.Status403Forbidden, await Invoke(endpoints, "Create", user, http, change, CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, await Invoke(endpoints, "CreateFamily", user, "ReadyToAcquire", change, http, CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, await Invoke(endpoints, "CreateScale", user, "DataAcquisitionWorker", new ScaleBody { Replicas = 1, Reason = "scale" }, http, CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, await Invoke(endpoints, "CreateAddBroker", user, reason, http, CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, await Invoke(endpoints, "CreateDecommission", user, 3, reason, http, CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, await Invoke(endpoints, "CreateRebalance", user, 3, reason, http, CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, await Invoke(endpoints, "Approve", user, id, CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, await Invoke(endpoints, "Reject", user, id, new RejectBody { Reason = "no" }, CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, await Invoke(endpoints, "Cancel", user, id, CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, await Invoke(endpoints, "Execute", user, id, CancellationToken.None));
    }

    [Fact]
    public async Task DescribeConfigsThrow_MakesRolesUnknown_AndRefusesDecommission()
    {
        var admin = new Mock<IAdminClient>();
        admin.Setup(client => client.DescribeConfigsAsync(It.IsAny<IEnumerable<ConfigResource>>(), It.IsAny<DescribeConfigsOptions>()))
            .ThrowsAsync(new KafkaException(ErrorCode.Local_Transport));
        var (ids, known) = await KafkaBrokerGateway.ControllerEligibleIdsAsync(admin.Object, RoleBrokers(), CancellationToken.None);

        Assert.False(known);
        Assert.Empty(ids);
        await AssertDecommissionRefusedWhenRolesAreUnknown();
    }

    [Fact]
    public async Task DescribeConfigsMissingBroker_MakesRolesUnknown_AndRefusesDecommission()
    {
        var admin = ConfigAdmin(_ => []);
        var (ids, known) = await KafkaBrokerGateway.ControllerEligibleIdsAsync(admin, RoleBrokers(), CancellationToken.None);

        Assert.False(known);
        Assert.Empty(ids);
        await AssertDecommissionRefusedWhenRolesAreUnknown();
    }

    [Fact]
    public async Task MissingProcessRoles_AreUnknown()
    {
        var admin = ConfigAdmin(resources =>
        [
            new DescribeConfigsResult
            {
                ConfigResource = new ConfigResource { Type = Confluent.Kafka.Admin.ResourceType.Broker, Name = resources.First().Name },
                Entries = new Dictionary<string, ConfigEntryResult>(StringComparer.OrdinalIgnoreCase)
            }
        ]);
        var (ids, known) = await KafkaBrokerGateway.ControllerEligibleIdsAsync(admin, RoleBrokers(), CancellationToken.None);

        Assert.False(known);
        Assert.Empty(ids);
    }

    [Fact]
    public async Task ReportedProcessRoles_MarkOnlyControllerBrokersEligible()
    {
        var admin = ConfigAdmin(resources =>
        [
            new DescribeConfigsResult
            {
                ConfigResource = new ConfigResource { Type = Confluent.Kafka.Admin.ResourceType.Broker, Name = resources.First().Name },
                Entries = new Dictionary<string, ConfigEntryResult>(StringComparer.OrdinalIgnoreCase)
                {
                    ["process.roles"] = new ConfigEntryResult
                    {
                        Name = "process.roles",
                        Value = resources.First().Name == "1" ? "broker,controller" : "broker"
                    }
                }
            }
        ]);
        var (ids, known) = await KafkaBrokerGateway.ControllerEligibleIdsAsync(admin, RoleBrokers(), CancellationToken.None);

        Assert.True(known);
        Assert.Equal([1], ids);
    }

    [Fact]
    public async Task Track_TimesOutAScaleWithoutSayingTheBrokerWasRemoved()
    {
        var (service, broker, infra) = NewService(requireSecondApprover: false, scaleTimeoutSeconds: 30);
        var created = await service.CreateScaleAsync(User("pat", ManageScaling), "DataAcquisitionWorker", 1, "add a worker", null, CancellationToken.None);
        await service.ExecuteAsync(User("pat", ManageScaling), created.Id, CancellationToken.None);
        var stored = (await service.GetAsync(created.Id, CancellationToken.None))!;
        stored.ExecutedUtc = DateTimeOffset.UtcNow.AddMinutes(-2);

        await service.TrackAsync(CancellationToken.None);

        Assert.Equal(KafkaChangeStatus.TimedOut, stored.Status);
        Assert.Contains("requested members", stored.Failure, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("broker was not removed", stored.Failure, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, infra.RemoveCalls);
    }

    [Fact]
    public async Task Track_TimesOutADecommissionWhenTheBrokerStays()
    {
        var (service, broker, infra) = NewService(requireSecondApprover: true, scaleTimeoutSeconds: 30);
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
        var created = await service.CreateDecommissionAsync(User("alice", ManageScaling), 3, "remove the spare broker", null, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), created.Id, CancellationToken.None);
        await service.ExecuteAsync(User("carol", ManageScaling), created.Id, CancellationToken.None);
        var stored = (await service.GetAsync(created.Id, CancellationToken.None))!;
        stored.ExecutedUtc = DateTimeOffset.UtcNow.AddMinutes(-2);

        await service.TrackAsync(CancellationToken.None);

        Assert.Equal(KafkaChangeStatus.TimedOut, stored.Status);
        Assert.Contains("broker was not removed", stored.Failure, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, infra.RemoveCalls);
    }

    [Fact]
    public async Task CompleteFamily_RaisesSiblingsOnly_AndDoesNotStartTheRateLimit()
    {
        var (service, broker, _) = NewService(requireSecondApprover: false);
        broker.SetCount("ReadyToAcquire", 4);
        broker.SetCount("ReadyToAcquire-Retry", 3);
        broker.SetCount("ReadyToAcquire-Error", 3);

        var plan = await service.PlanFamilyAsync("ReadyToAcquire", false, null, CancellationToken.None);
        Assert.True(plan.Accepted);
        Assert.Equal(4, plan.CurrentPartitions);
        Assert.Equal(4, plan.RequestedPartitions);
        Assert.Equal(["ReadyToAcquire-Retry", "ReadyToAcquire-Error"], plan.TopicsToRaise);
        Assert.DoesNotContain("ReadyToAcquire", plan.TopicsToRaise);

        var created = await service.CreateFamilyAsync(User("alice", ManageTopics), "ReadyToAcquire", "catch the family up", false, null, "ReadyToAcquire", null, CancellationToken.None);
        Assert.False(created.SecondApproverRequired);
        Assert.Equal(KafkaChangeStatus.Approved, created.Status);
        var executed = await service.ExecuteAsync(User("alice", ManageTopics), created.Id, CancellationToken.None);

        Assert.Equal(KafkaChangeStatus.Converging, executed.Status);
        Assert.Null(executed.PartitionsChangedUtc);
        Assert.DoesNotContain(broker.Increases, increase => increase.Topic == "ReadyToAcquire");
        Assert.Contains(broker.Increases, increase => increase.Topic == "ReadyToAcquire-Retry" && increase.Count == 4);
        Assert.Contains(broker.Increases, increase => increase.Topic == "ReadyToAcquire-Error" && increase.Count == 4);
        Assert.Equal(4, broker.Increases.Single(increase => increase.Topic == "ReadyToAcquire-Retry").Count);
    }

    [Fact]
    public async Task CompleteFamily_RefusesAnEvenFamily()
    {
        var (service, broker, _) = NewService(requireSecondApprover: false);
        broker.SetCount("ReadyToAcquire", 4);
        broker.SetCount("ReadyToAcquire-Retry", 4);
        broker.SetCount("ReadyToAcquire-Error", 4);

        var plan = await service.PlanFamilyAsync("ReadyToAcquire", false, null, CancellationToken.None);
        Assert.False(plan.Accepted);
        Assert.Contains(plan.Errors, error => error.Contains("No sibling is behind", StringComparison.Ordinal));
        var refused = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.CreateFamilyAsync(User("alice", ManageTopics), "ReadyToAcquire", "catch the family up", false, null, "ReadyToAcquire", null, CancellationToken.None));
        Assert.Contains("No sibling is behind", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReassignmentInFlight_RefusesPartitionRebalanceDecommissionAndBrokerAdd()
    {
        var (service, broker, _) = NewService(requireSecondApprover: true);
        var pending = await service.CreateAsync(User("alice", ManageTopics), "ReadyToAcquire", 4, "raise the log topic", false, null, "ReadyToAcquire", null, CancellationToken.None);
        broker.Reassignments = new ReassignmentListing { Known = true, Topics = ["ops-proof-log"] };

        var plan = await service.PlanAsync("ReportScheduled", 4, false, null, CancellationToken.None);
        Assert.Contains(plan.Errors, error => error.Contains("ops-proof-log", StringComparison.Ordinal));
        var family = await service.PlanFamilyAsync("ReadyToAcquire", false, null, CancellationToken.None);
        Assert.Contains(family.Errors, error => error.Contains("ops-proof-log", StringComparison.Ordinal));

        var create = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.CreateAsync(User("alice", ManageTopics), "ReportScheduled", 4, "raise it", false, null, "ReportScheduled", null, CancellationToken.None));
        Assert.Contains("ops-proof-log", create.Message, StringComparison.Ordinal);

        var decommission = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() => service.PlanDecommissionAsync(3, CancellationToken.None));
        Assert.Contains("ops-proof-log", decommission.Message, StringComparison.Ordinal);
        var rebalance = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() => service.PlanRebalanceAsync(3, CancellationToken.None));
        Assert.Contains("ops-proof-log", rebalance.Message, StringComparison.Ordinal);
        var add = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.CreateAddBrokerAsync(User("pat", ManageScaling), "add a broker", null, CancellationToken.None));
        Assert.Contains("ops-proof-log", add.Message, StringComparison.Ordinal);

        var approve = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ApproveAsync(User("bob", ManageTopics), pending.Id, CancellationToken.None));
        Assert.Contains("ops-proof-log", approve.Message, StringComparison.Ordinal);
        var execute = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ExecuteAsync(User("carol", ManageTopics), pending.Id, CancellationToken.None));
        Assert.Contains("ops-proof-log", execute.Message, StringComparison.Ordinal);
        Assert.Equal(KafkaChangeStatus.Pending, (await service.GetAsync(pending.Id, CancellationToken.None))!.Status);

        broker.Reassignments = new ReassignmentListing { Known = true };
        var scale = await service.PlanScaleAsync("DataAcquisitionWorker", 1, CancellationToken.None);
        Assert.DoesNotContain(scale.Errors, error => error.Contains("reassignment", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UnknownReassignment_RefusesMovesAndCancelExecution_AndStillAllowsScale()
    {
        var (service, broker, infra) = NewService(requireSecondApprover: false);
        broker.Reassignments = new ReassignmentListing { Known = false };
        infra.Reassignments = new ReassignmentListing { Known = false };

        var plan = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.PlanAsync("ReportScheduled", 4, false, null, CancellationToken.None));
        Assert.Contains("cannot confirm no reassignment is in flight", plan.Message, StringComparison.OrdinalIgnoreCase);
        var family = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.PlanFamilyAsync("ReadyToAcquire", false, null, CancellationToken.None));
        Assert.Contains("cannot confirm no reassignment is in flight", family.Message, StringComparison.OrdinalIgnoreCase);
        var decommission = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.PlanDecommissionAsync(3, CancellationToken.None));
        Assert.Contains("cannot confirm no reassignment is in flight", decommission.Message, StringComparison.OrdinalIgnoreCase);
        var rebalance = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.PlanRebalanceAsync(3, CancellationToken.None));
        Assert.Contains("cannot confirm no reassignment is in flight", rebalance.Message, StringComparison.OrdinalIgnoreCase);
        var add = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.CreateAddBrokerAsync(User("pat", ManageScaling), "add a broker", null, CancellationToken.None));
        Assert.Contains("cannot confirm no reassignment is in flight", add.Message, StringComparison.OrdinalIgnoreCase);
        var create = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.CreateAsync(User("alice", ManageTopics), "ReportScheduled", 4, "raise it", false, null, "ReportScheduled", null, CancellationToken.None));
        Assert.Contains("cannot confirm no reassignment is in flight", create.Message, StringComparison.OrdinalIgnoreCase);

        var scalePlan = await service.PlanScaleAsync("DataAcquisitionWorker", 1, CancellationToken.None);
        Assert.DoesNotContain(scalePlan.Errors, error => error.Contains("reassignment", StringComparison.OrdinalIgnoreCase));
        var (scaling, scalingBroker, scalingInfra) = NewService(requireSecondApprover: false);
        scalingBroker.Reassignments = new ReassignmentListing { Known = false };
        scalingInfra.Reassignments = new ReassignmentListing { Known = false };
        var scale = await scaling.CreateScaleAsync(User("pat", ManageScaling), "DataAcquisitionWorker", 1, "add a worker", null, CancellationToken.None);
        var executedScale = await scaling.ExecuteAsync(User("pat", ManageScaling), scale.Id, CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.Converging, executedScale.Status);

        broker.Reassignments = new ReassignmentListing { Known = true };
        infra.Reassignments = new ReassignmentListing { Known = true };
        broker.ControllerId = 2;
        broker.Eligible.Clear();
        broker.Eligible.Add(0);
        broker.RolesKnown = true;
        broker.Placements.Add(new PartitionPlacement { Topic = "ReadyToAcquire", Partition = 0, Leader = 1, Replicas = [1, 2, 3], Isr = [1, 2, 3] });
        var moving = await service.CreateDecommissionAsync(User("alice", ManageScaling), 3, "remove the spare broker", null, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), moving.Id, CancellationToken.None);
        await service.ExecuteAsync(User("carol", ManageScaling), moving.Id, CancellationToken.None);

        broker.Reassignments = new ReassignmentListing { Known = false };
        infra.Reassignments = new ReassignmentListing { Known = false };
        var cancel = await service.CancelAsync(User("alice", ManageScaling), moving.Id, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), cancel.Id, CancellationToken.None);
        var refusedCancel = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ExecuteAsync(User("carol", ManageScaling), cancel.Id, CancellationToken.None));
        Assert.Contains("cannot confirm no reassignment is in flight", refusedCancel.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, infra.CancelCalls);
        Assert.Equal(KafkaChangeStatus.Converging, (await service.GetAsync(moving.Id, CancellationToken.None))!.Status);

        broker.Reassignments = new ReassignmentListing { Known = false };
        infra.Reassignments = new ReassignmentListing { Known = true, Topics = ["ops-proof-log"] };
        var named = await service.PlanAsync("ReportScheduled", 4, false, null, CancellationToken.None);
        Assert.Contains(named.Errors, error => error.Contains("ops-proof-log", StringComparison.Ordinal));
        Assert.DoesNotContain(named.Errors, error => error.Contains("cannot confirm", StringComparison.OrdinalIgnoreCase));

        broker.Reassignments = new ReassignmentListing { Known = true, Topics = ["ops-proof-members"] };
        infra.Reassignments = new ReassignmentListing { Known = false };
        var fromBroker = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.CreateAsync(User("alice", ManageTopics), "ReportScheduled", 4, "raise it", false, null, "ReportScheduled", null, CancellationToken.None));
        Assert.Contains("ops-proof-members", fromBroker.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisabledProvider_RefusesMovesUntilAnInfraProviderIsEnabled()
    {
        var (service, broker, infra) = NewService(requireSecondApprover: false);
        broker.Reassignments = new ReassignmentListing { Known = false };
        infra.Reassignments = new ReassignmentListing { Known = false };
        infra.Enabled = false;
        infra.Name = "Disabled";

        var plan = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.PlanAsync("ReportScheduled", 4, false, null, CancellationToken.None));
        Assert.Contains("cannot confirm no reassignment is in flight", plan.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("enable an infra provider to allow this", plan.Message, StringComparison.OrdinalIgnoreCase);

        var family = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.PlanFamilyAsync("ReadyToAcquire", false, null, CancellationToken.None));
        Assert.Contains("enable an infra provider to allow this", family.Message, StringComparison.OrdinalIgnoreCase);
        var decommission = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.PlanDecommissionAsync(3, CancellationToken.None));
        Assert.Contains("enable an infra provider to allow this", decommission.Message, StringComparison.OrdinalIgnoreCase);
        var rebalance = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.PlanRebalanceAsync(3, CancellationToken.None));
        Assert.Contains("enable an infra provider to allow this", rebalance.Message, StringComparison.OrdinalIgnoreCase);
        var add = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.CreateAddBrokerAsync(User("pat", ManageScaling), "add a broker", null, CancellationToken.None));
        Assert.Contains("enable an infra provider to allow this", add.Message, StringComparison.OrdinalIgnoreCase);

        var scale = await service.PlanScaleAsync("DataAcquisitionWorker", 1, CancellationToken.None);
        Assert.DoesNotContain(scale.Errors, error => error.Contains("infra provider", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TimedOutDecommission_StaysOpenWithoutStoppingOrCancelling()
    {
        var (service, broker, infra) = NewService(requireSecondApprover: true, scaleTimeoutSeconds: 30);
        broker.ControllerId = 2;
        broker.Eligible.Clear();
        broker.Eligible.Add(0);
        broker.RolesKnown = true;
        broker.Placements.Add(new PartitionPlacement { Topic = "ReadyToAcquire", Partition = 0, Leader = 1, Replicas = [1, 2, 3], Isr = [1, 2, 3] });
        var created = await service.CreateDecommissionAsync(User("alice", ManageScaling), 3, "remove the spare broker", null, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), created.Id, CancellationToken.None);
        await service.ExecuteAsync(User("carol", ManageScaling), created.Id, CancellationToken.None);
        var stored = (await service.GetAsync(created.Id, CancellationToken.None))!;
        stored.ExecutedUtc = DateTimeOffset.UtcNow.AddMinutes(-2);

        await service.TrackAsync(CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.TimedOut, stored.Status);
        Assert.Contains("broker was not removed", stored.Failure, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, infra.CancelCalls);
        Assert.Equal(0, infra.RemoveCalls);

        broker.Reassignments = new ReassignmentListing { Known = false };
        infra.Reassignments = new ReassignmentListing { Known = false };
        await service.TrackAsync(CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.TimedOut, stored.Status);
        Assert.Equal(0, infra.CancelCalls);
        Assert.Equal(0, infra.RemoveCalls);

        broker.Reassignments = new ReassignmentListing { Known = true };
        var blocked = await service.PlanAsync("ReportScheduled", 4, false, null, CancellationToken.None);
        Assert.Contains(blocked.Errors, error => error.Contains("ReadyToAcquire", StringComparison.Ordinal));

        await service.TrackAsync(CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.Failed, stored.Status);
        Assert.Contains("ended before the replicas matched", stored.Failure, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, infra.RemoveCalls);
        Assert.Equal(0, infra.CancelCalls);
    }

    [Fact]
    public async Task TimedOutDecommission_FailsWhenTheProviderListIsEmpty()
    {
        var (service, broker, infra) = NewService(requireSecondApprover: true, scaleTimeoutSeconds: 30);
        broker.ControllerId = 2;
        broker.Eligible.Clear();
        broker.Eligible.Add(0);
        broker.RolesKnown = true;
        broker.Placements.Add(new PartitionPlacement { Topic = "ReadyToAcquire", Partition = 0, Leader = 1, Replicas = [1, 2, 3], Isr = [1, 2, 3] });
        var created = await service.CreateDecommissionAsync(User("alice", ManageScaling), 3, "remove the spare broker", null, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), created.Id, CancellationToken.None);
        await service.ExecuteAsync(User("carol", ManageScaling), created.Id, CancellationToken.None);
        var stored = (await service.GetAsync(created.Id, CancellationToken.None))!;
        stored.ExecutedUtc = DateTimeOffset.UtcNow.AddMinutes(-2);

        await service.TrackAsync(CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.TimedOut, stored.Status);

        broker.Reassignments = new ReassignmentListing { Known = false };
        infra.Reassignments = new ReassignmentListing { Known = true };
        await service.TrackAsync(CancellationToken.None);

        Assert.Equal(KafkaChangeStatus.Failed, stored.Status);
        Assert.Contains("ended before the replicas matched", stored.Failure, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, infra.RemoveCalls);
        Assert.Equal(0, infra.CancelCalls);
    }

    [Fact]
    public async Task TimedOutDecommission_CompletesWhenTheReplicasFinishMoving()
    {
        var (service, broker, infra) = NewService(requireSecondApprover: true, scaleTimeoutSeconds: 30);
        broker.ControllerId = 2;
        broker.Eligible.Clear();
        broker.Eligible.Add(0);
        broker.RolesKnown = true;
        broker.Placements.Add(new PartitionPlacement { Topic = "ReadyToAcquire", Partition = 0, Leader = 1, Replicas = [1, 2, 3], Isr = [1, 2, 3] });
        var created = await service.CreateDecommissionAsync(User("alice", ManageScaling), 3, "remove the spare broker", null, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), created.Id, CancellationToken.None);
        await service.ExecuteAsync(User("carol", ManageScaling), created.Id, CancellationToken.None);
        var stored = (await service.GetAsync(created.Id, CancellationToken.None))!;
        stored.ExecutedUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
        broker.Reassignments = new ReassignmentListing { Known = false };

        await service.TrackAsync(CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.TimedOut, stored.Status);

        broker.Placements.Clear();
        await service.TrackAsync(CancellationToken.None);

        Assert.Equal(KafkaChangeStatus.Done, stored.Status);
        Assert.Equal(0, infra.RemoveCalls);
        Assert.Equal(0, infra.CancelCalls);
        Assert.Contains("not stopped", stored.Progress, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelReassignment_NeedsAnotherPerson_AndChecksTheOriginalReplicas()
    {
        var (service, broker, infra) = NewService(requireSecondApprover: true);
        var endpoints = new KafkaOpsEndpoints(service, NullLogger<KafkaOpsEndpoints>.Instance);
        broker.ControllerId = 2;
        broker.Eligible.Clear();
        broker.Eligible.Add(0);
        broker.RolesKnown = true;
        broker.Placements.Add(new PartitionPlacement { Topic = "ReadyToAcquire", Partition = 0, Leader = 1, Replicas = [1, 2, 3], Isr = [1, 2, 3] });
        var created = await service.CreateDecommissionAsync(User("alice", ManageScaling), 3, "remove the spare broker", null, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), created.Id, CancellationToken.None);
        await service.ExecuteAsync(User("carol", ManageScaling), created.Id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, await StatusAsync(endpoints, "Cancel", User("sam", ManageTopics), created.Id, null));
        var cancel = await service.CancelAsync(User("alice", ManageScaling), created.Id, CancellationToken.None);
        Assert.Equal(KafkaChangeKind.CancelReassignment, cancel.Kind);
        Assert.Equal(KafkaChangeStatus.Pending, cancel.Status);
        var self = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ApproveAsync(User("alice", ManageScaling), cancel.Id, CancellationToken.None));
        Assert.Contains("cannot approve", self.Message, StringComparison.OrdinalIgnoreCase);
        await service.ApproveAsync(User("bob", ManageScaling), cancel.Id, CancellationToken.None);

        broker.Reassignments = new ReassignmentListing { Known = true, Topics = ["ReadyToAcquire"] };
        broker.Placements.Clear();
        broker.Placements.Add(new PartitionPlacement { Topic = "ReadyToAcquire", Partition = 0, Leader = 0, Replicas = [0, 1, 2], Isr = [0, 1, 2] });
        var mismatch = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ExecuteAsync(User("carol", ManageScaling), cancel.Id, CancellationToken.None));
        Assert.Contains("did not return", mismatch.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(KafkaChangeStatus.Failed, (await service.GetAsync(cancel.Id, CancellationToken.None))!.Status);
        Assert.Equal(1, infra.CancelCalls);

        broker.Placements.Clear();
        broker.Placements.Add(new PartitionPlacement { Topic = "ReadyToAcquire", Partition = 0, Leader = 1, Replicas = [1, 2, 3], Isr = [1, 2, 3] });
        var again = await service.CancelAsync(User("alice", ManageScaling), created.Id, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), again.Id, CancellationToken.None);
        var done = await service.ExecuteAsync(User("carol", ManageScaling), again.Id, CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.Cancelled, done.Status);
        Assert.Contains("original assignment", done.Progress, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(KafkaChangeStatus.Cancelled, (await service.GetAsync(created.Id, CancellationToken.None))!.Status);
        Assert.Equal(2, infra.CancelCalls);
        Assert.Equal(created.RebalanceName, infra.LastRebalanceName);
    }

    [Fact]
    public async Task Cancel_RevertsOnlyThePartitionsStillInFlight()
    {
        var (service, broker, infra) = NewService(requireSecondApprover: true);
        broker.ControllerId = 2;
        broker.Eligible.Clear();
        broker.Eligible.Add(0);
        broker.RolesKnown = true;
        broker.Placements.Add(new PartitionPlacement { Topic = "ReadyToAcquire", Partition = 0, Leader = 1, Replicas = [1, 2, 3], Isr = [1, 2, 3] });
        broker.Placements.Add(new PartitionPlacement { Topic = "ReadyToAcquire", Partition = 1, Leader = 1, Replicas = [1, 2, 3], Isr = [1, 2, 3] });
        var created = await service.CreateDecommissionAsync(User("alice", ManageScaling), 3, "remove the spare broker", null, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), created.Id, CancellationToken.None);
        await service.ExecuteAsync(User("carol", ManageScaling), created.Id, CancellationToken.None);
        var stored = (await service.GetAsync(created.Id, CancellationToken.None))!;

        broker.Reassignments = new ReassignmentListing { Known = false };
        infra.Reassignments = new ReassignmentListing { Known = false };
        var cancel = await service.CancelAsync(User("alice", ManageScaling), created.Id, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), cancel.Id, CancellationToken.None);
        var unknown = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ExecuteAsync(User("carol", ManageScaling), cancel.Id, CancellationToken.None));
        Assert.Contains("cannot confirm no reassignment is in flight", unknown.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, infra.CancelCalls);
        Assert.Equal(KafkaChangeStatus.Converging, stored.Status);

        var again = await service.CancelAsync(User("alice", ManageScaling), created.Id, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), again.Id, CancellationToken.None);
        broker.Reassignments = new ReassignmentListing
        {
            Known = true,
            Topics = ["ReadyToAcquire"],
            Partitions = ["ReadyToAcquire\n0"]
        };
        broker.Placements.Clear();
        ApplyAssignment(broker, stored.OriginalAssignmentJson, (topic, partition) => topic == "ReadyToAcquire" && partition == 0);
        ApplyAssignment(broker, stored.ReassignmentJson, (topic, partition) => topic == "ReadyToAcquire" && partition == 1);

        var done = await service.ExecuteAsync(User("carol", ManageScaling), again.Id, CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.Cancelled, done.Status);
        Assert.Contains("1 partitions had already moved and keep their new replicas.", done.Progress, StringComparison.Ordinal);
        Assert.Contains("The broker was not stopped.", done.Progress, StringComparison.Ordinal);
        Assert.Equal(KafkaChangeStatus.Cancelled, stored.Status);
        Assert.Equal(1, infra.CancelCalls);
        Assert.Equal(1, infra.ReleaseCalls);
        Assert.Equal(created.RebalanceName, infra.LastRebalanceName);
    }

    [Fact]
    public async Task Cancel_AcceptsAPartitionThatLandsOnTheTargetDuringTheCancel()
    {
        var (service, broker, infra) = NewService(requireSecondApprover: true);
        broker.ControllerId = 2;
        broker.Eligible.Clear();
        broker.Eligible.Add(0);
        broker.RolesKnown = true;
        broker.Placements.Add(new PartitionPlacement { Topic = "ReadyToAcquire", Partition = 0, Leader = 1, Replicas = [1, 2, 3], Isr = [1, 2, 3] });
        broker.Placements.Add(new PartitionPlacement { Topic = "ReadyToAcquire", Partition = 1, Leader = 1, Replicas = [1, 2, 3], Isr = [1, 2, 3] });
        var created = await service.CreateDecommissionAsync(User("alice", ManageScaling), 3, "remove the spare broker", null, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), created.Id, CancellationToken.None);
        await service.ExecuteAsync(User("carol", ManageScaling), created.Id, CancellationToken.None);
        var stored = (await service.GetAsync(created.Id, CancellationToken.None))!;

        var cancel = await service.CancelAsync(User("alice", ManageScaling), created.Id, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), cancel.Id, CancellationToken.None);
        broker.Reassignments = new ReassignmentListing
        {
            Known = true,
            Topics = ["ReadyToAcquire"],
            Partitions = ["ReadyToAcquire\n0", "ReadyToAcquire\n1"]
        };
        broker.Placements.Clear();
        ApplyAssignment(broker, stored.OriginalAssignmentJson, (topic, partition) => topic == "ReadyToAcquire" && partition == 0);
        ApplyAssignment(broker, stored.ReassignmentJson, (topic, partition) => topic == "ReadyToAcquire" && partition == 1);

        var done = await service.ExecuteAsync(User("carol", ManageScaling), cancel.Id, CancellationToken.None);
        Assert.Equal(KafkaChangeStatus.Cancelled, done.Status);
        Assert.Contains("1 partitions had already moved and keep their new replicas.", done.Progress, StringComparison.Ordinal);
        Assert.Contains("The broker was not stopped.", done.Progress, StringComparison.Ordinal);
        Assert.Equal(KafkaChangeStatus.Cancelled, stored.Status);
        Assert.Equal(1, infra.CancelCalls);
        Assert.Equal(1, infra.ReleaseCalls);
    }

    [Fact]
    public async Task TwoRebalanceRequests_GetDistinctNames()
    {
        var (service, broker, _) = NewService(requireSecondApprover: true);
        broker.ControllerId = 2;
        broker.Eligible.Clear();
        broker.Eligible.Add(0);
        broker.RolesKnown = true;
        broker.Placements.Add(new PartitionPlacement { Topic = "ReadyToAcquire", Partition = 0, Leader = 1, Replicas = [1, 2, 3], Isr = [1, 2, 3] });
        var first = await service.CreateDecommissionAsync(User("alice", ManageScaling), 3, "remove the spare broker", null, CancellationToken.None);
        await service.RejectAsync(User("bob", ManageScaling), first.Id, "not now", CancellationToken.None);
        var second = await service.CreateDecommissionAsync(User("alice", ManageScaling), 3, "remove the spare broker", null, CancellationToken.None);

        Assert.NotEqual(first.RebalanceName, second.RebalanceName);
        Assert.Equal(KafkaRebalanceNames.For(first.Id), first.RebalanceName);
        Assert.Equal(KafkaRebalanceNames.For(second.Id), second.RebalanceName);
    }

    [Fact]
    public async Task OrderSensitiveFamilyCompletion_RequiresASecondApprover()
    {
        var (service, broker, _) = NewService(requireSecondApprover: false);
        broker.SetCount("PatientEvent", 4);
        broker.SetCount("PatientEvent-Retry", 3);
        broker.SetCount("PatientEvent-Error", 3);

        var plan = await service.PlanFamilyAsync("PatientEvent", true, "the window was observed outside this request", CancellationToken.None);
        Assert.True(plan.Accepted, string.Join(" ", plan.Errors));
        Assert.True(plan.SecondApproverRequired);

        var created = await service.CreateFamilyAsync(
            User("alice", ManageTopics),
            "PatientEvent",
            "catch the family up",
            true,
            "the window was observed outside this request",
            "PatientEvent",
            null,
            CancellationToken.None);
        Assert.True(created.SecondApproverRequired);
        Assert.Equal(KafkaChangeStatus.Pending, created.Status);
        var self = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ApproveAsync(User("alice", ManageTopics), created.Id, CancellationToken.None));
        Assert.Contains("cannot approve", self.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PartitionDecommissionAndCancel_RequireASecondApproverWhenTheOptionIsOff()
    {
        var (service, broker, _) = NewService(requireSecondApprover: false);
        broker.SetCount("ReadyToAcquire", 3);
        var created = await service.CreateAsync(User("alice", ManageTopics), "ReadyToAcquire", 4, "raise the log topic", false, null, "ReadyToAcquire", null, CancellationToken.None);
        Assert.True(created.SecondApproverRequired);
        Assert.Equal(KafkaChangeStatus.Pending, created.Status);
        var self = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ApproveAsync(User("alice", ManageTopics), created.Id, CancellationToken.None));
        Assert.Contains("cannot approve", self.Message, StringComparison.OrdinalIgnoreCase);
        await service.RejectAsync(User("bob", ManageTopics), created.Id, "not now", CancellationToken.None);

        broker.ControllerId = 2;
        broker.Eligible.Clear();
        broker.Eligible.Add(0);
        broker.RolesKnown = true;
        broker.Placements.Add(new PartitionPlacement { Topic = "ReadyToAcquire", Partition = 0, Leader = 1, Replicas = [1, 2, 3], Isr = [1, 2, 3] });
        var decommission = await service.CreateDecommissionAsync(User("alice", ManageScaling), 3, "remove the spare broker", null, CancellationToken.None);
        Assert.True(decommission.SecondApproverRequired);
        var ownDecommission = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ApproveAsync(User("alice", ManageScaling), decommission.Id, CancellationToken.None));
        Assert.Contains("cannot approve", ownDecommission.Message, StringComparison.OrdinalIgnoreCase);
        await service.ApproveAsync(User("bob", ManageScaling), decommission.Id, CancellationToken.None);
        await service.ExecuteAsync(User("carol", ManageScaling), decommission.Id, CancellationToken.None);

        var cancel = await service.CancelAsync(User("alice", ManageScaling), decommission.Id, CancellationToken.None);
        Assert.True(cancel.SecondApproverRequired);
        var ownCancel = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ApproveAsync(User("alice", ManageScaling), cancel.Id, CancellationToken.None));
        Assert.Contains("cannot approve", ownCancel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddBrokerPlan_RefusesWhileAMoveIsUnknownOrInFlight()
    {
        var (service, broker, infra) = NewService(requireSecondApprover: true);
        var endpoints = new KafkaOpsEndpoints(service, NullLogger<KafkaOpsEndpoints>.Instance);
        var viewer = User("pat", nameof(LinkSystemPermissions.CanViewInfrastructure));
        Assert.Equal(StatusCodes.Status200OK, await Invoke(endpoints, "PlanAddBroker", viewer, CancellationToken.None));

        broker.Reassignments = new ReassignmentListing { Known = false };
        infra.Reassignments = new ReassignmentListing { Known = false };
        Assert.Equal(StatusCodes.Status400BadRequest, await Invoke(endpoints, "PlanAddBroker", viewer, CancellationToken.None));
        var unknown = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.PlanAddBrokerAsync(CancellationToken.None));
        Assert.Contains("cannot confirm no reassignment is in flight", unknown.Message, StringComparison.OrdinalIgnoreCase);

        broker.Reassignments = new ReassignmentListing { Known = true, Topics = ["ReadyToAcquire"] };
        var busy = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.PlanAddBrokerAsync(CancellationToken.None));
        Assert.Contains("ReadyToAcquire", busy.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DescribeConsumerGroupsException_WithNotCoordinator_RebuildsTheClient()
    {
        var created = 0;
        var broken = new Mock<IAdminClient>();
        broken.Setup(client => client.DescribeConsumerGroupsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DescribeConsumerGroupsOptions>()))
            .ThrowsAsync(new DescribeConsumerGroupsException(new DescribeConsumerGroupsReport
            {
                ConsumerGroupDescriptions =
                [
                    new ConsumerGroupDescription { GroupId = "Report", Error = new Error(ErrorCode.NotCoordinatorForGroup) }
                ]
            }));
        var healthy = new Mock<IAdminClient>();
        healthy.Setup(client => client.DescribeConsumerGroupsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DescribeConsumerGroupsOptions>()))
            .ReturnsAsync(new DescribeConsumerGroupsResult());
        var factory = new Mock<IKafkaAdminClientFactory>();
        factory.Setup(item => item.Create(It.IsAny<AdminClientConfig>()))
            .Returns(() => ++created == 1 ? broken.Object : healthy.Object);
        var gateway = new KafkaBrokerGateway(new KafkaConnection { BootstrapServers = ["localhost:9092"] }, factory.Object);

        var missing = await Assert.ThrowsAsync<KafkaException>(() => gateway.DescribeGroupsAsync(false, 1, CancellationToken.None));
        Assert.Equal(ErrorCode.NotCoordinatorForGroup, missing.Error.Code);
        Assert.Contains("Report", missing.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ErrorCode.NotCoordinatorForGroup), missing.Message, StringComparison.Ordinal);
        Assert.Equal(2, created);
        broken.Verify(client => client.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GroupError_NamesTheGroupAndSaysWhenTheBrokerGaveNoDetail()
    {
        var admin = new Mock<IAdminClient>();
        admin.Setup(client => client.DescribeConsumerGroupsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DescribeConsumerGroupsOptions>()))
            .ThrowsAsync(new DescribeConsumerGroupsException(new DescribeConsumerGroupsReport
            {
                ConsumerGroupDescriptions =
                [
                    new ConsumerGroupDescription
                    {
                        GroupId = "Report",
                        Error = new Error(ErrorCode.NotCoordinatorForGroup, "ConsumerGroupDescribe:")
                    }
                ]
            }));
        var factory = new Mock<IKafkaAdminClientFactory>();
        factory.Setup(item => item.Create(It.IsAny<AdminClientConfig>())).Returns(admin.Object);
        var gateway = new KafkaBrokerGateway(new KafkaConnection { BootstrapServers = ["localhost:9092"] }, factory.Object);

        var error = await Assert.ThrowsAsync<KafkaException>(() => gateway.DescribeGroupsAsync(false, 1, CancellationToken.None));
        Assert.Equal(ErrorCode.NotCoordinatorForGroup, error.Error.Code);
        Assert.Contains("Report", error.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ErrorCode.NotCoordinatorForGroup), error.Message, StringComparison.Ordinal);
        Assert.Contains("no error detail returned by broker", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RoleLookup_UsesAFifteenSecondTimeout()
    {
        DescribeConfigsOptions? seen = null;
        var admin = new Mock<IAdminClient>();
        admin.Setup(client => client.DescribeConfigsAsync(It.IsAny<IEnumerable<ConfigResource>>(), It.IsAny<DescribeConfigsOptions>()))
            .Callback<IEnumerable<ConfigResource>, DescribeConfigsOptions>((_, options) => seen = options)
            .ReturnsAsync([]);
        await KafkaBrokerGateway.ControllerEligibleIdsAsync(admin.Object, RoleBrokers(), CancellationToken.None);

        Assert.NotNull(seen);
        Assert.Equal(TimeSpan.FromSeconds(15), seen!.RequestTimeout);
    }

    [Fact]
    public async Task PreferredElection_UsesPreferredOnTheAdminPath_AndWarnsWithoutFailing()
    {
        Assert.Equal(ElectionType.Preferred, KafkaLeaderElection.Kind);
        Assert.NotEqual(ElectionType.Unclean, KafkaLeaderElection.Kind);

        var (service, broker, infra) = NewService(requireSecondApprover: true);
        infra.Name = "LocalCompose";
        broker.Placements.Add(new PartitionPlacement { Topic = "ReadyToAcquire", Partition = 0, Leader = 1, Replicas = [1], Isr = [1] });
        var created = await service.CreateRebalanceAsync(User("alice", ManageScaling), 3, "spread onto the new broker", null, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), created.Id, CancellationToken.None);
        var executed = await service.ExecuteAsync(User("carol", ManageScaling), created.Id, CancellationToken.None);
        ReplacePlacements(broker, executed.ReassignmentJson);
        await service.TrackAsync(CancellationToken.None);
        var stored = (await service.GetAsync(created.Id, CancellationToken.None))!;
        Assert.Equal(KafkaChangeStatus.Done, stored.Status);
        Assert.Equal([ElectionType.Preferred], broker.Elections);
        Assert.Contains("Preferred leaders", stored.Progress, StringComparison.Ordinal);
        Assert.Equal("", stored.Warning);
        Assert.Equal(1, infra.ReleaseCalls);

        infra.Name = "Strimzi";
        broker.Elections.Clear();
        broker.Placements.Clear();
        broker.Placements.Add(new PartitionPlacement { Topic = "ReadyToAcquire", Partition = 0, Leader = 1, Replicas = [1], Isr = [1] });
        var skipped = await service.CreateRebalanceAsync(User("alice", ManageScaling), 3, "spread onto the new broker", null, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), skipped.Id, CancellationToken.None);
        var skippedRun = await service.ExecuteAsync(User("carol", ManageScaling), skipped.Id, CancellationToken.None);
        ReplacePlacements(broker, skippedRun.ReassignmentJson);
        await service.TrackAsync(CancellationToken.None);
        var skippedStored = (await service.GetAsync(skipped.Id, CancellationToken.None))!;
        Assert.Equal(KafkaChangeStatus.Done, skippedStored.Status);
        Assert.Empty(broker.Elections);
        Assert.Contains("skipped", skippedStored.Progress, StringComparison.OrdinalIgnoreCase);

        infra.Name = "LocalCompose";
        broker.ElectionError = new InvalidOperationException("election refused");
        broker.Placements.Clear();
        broker.Placements.Add(new PartitionPlacement { Topic = "ReadyToAcquire", Partition = 0, Leader = 1, Replicas = [1], Isr = [1] });
        var warned = await service.CreateRebalanceAsync(User("alice", ManageScaling), 3, "spread onto the new broker", null, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), warned.Id, CancellationToken.None);
        var warnedRun = await service.ExecuteAsync(User("carol", ManageScaling), warned.Id, CancellationToken.None);
        ReplacePlacements(broker, warnedRun.ReassignmentJson);
        await service.TrackAsync(CancellationToken.None);
        var warnedStored = (await service.GetAsync(warned.Id, CancellationToken.None))!;
        Assert.Equal(KafkaChangeStatus.Done, warnedStored.Status);
        Assert.Contains("did not finish", warnedStored.Warning, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("", warnedStored.Failure);
    }

    private static void ApplyAssignment(FakeBroker broker, string json, Func<string, int, bool> take)
    {
        using var document = JsonDocument.Parse(json);
        foreach (var partition in document.RootElement.GetProperty("partitions").EnumerateArray())
        {
            var topic = partition.GetProperty("topic").GetString() ?? "";
            var id = partition.GetProperty("partition").GetInt32();
            if (!take(topic, id))
                continue;
            broker.Placements.Add(new PartitionPlacement
            {
                Topic = topic,
                Partition = id,
                Leader = partition.GetProperty("replicas").EnumerateArray().First().GetInt32(),
                Replicas = partition.GetProperty("replicas").EnumerateArray().Select(item => item.GetInt32()).ToList(),
                Isr = partition.GetProperty("replicas").EnumerateArray().Select(item => item.GetInt32()).ToList()
            });
        }
    }

    private static void ReplacePlacements(FakeBroker broker, string reassignmentJson)
    {
        broker.Placements.Clear();
        using var document = JsonDocument.Parse(reassignmentJson);
        foreach (var partition in document.RootElement.GetProperty("partitions").EnumerateArray())
        {
            broker.Placements.Add(new PartitionPlacement
            {
                Topic = partition.GetProperty("topic").GetString() ?? "",
                Partition = partition.GetProperty("partition").GetInt32(),
                Leader = partition.GetProperty("replicas").EnumerateArray().First().GetInt32(),
                Replicas = partition.GetProperty("replicas").EnumerateArray().Select(item => item.GetInt32()).ToList(),
                Isr = partition.GetProperty("replicas").EnumerateArray().Select(item => item.GetInt32()).ToList()
            });
        }
    }

    private static List<BrokerSnapshot> RoleBrokers() =>
    [
        new BrokerSnapshot { Id = 1, State = "up" },
        new BrokerSnapshot { Id = 3, State = "up" }
    ];

    private static IAdminClient ConfigAdmin(Func<IEnumerable<ConfigResource>, List<DescribeConfigsResult>> describe)
    {
        var admin = new Mock<IAdminClient>();
        admin.Setup(client => client.DescribeConfigsAsync(It.IsAny<IEnumerable<ConfigResource>>(), It.IsAny<DescribeConfigsOptions>()))
            .Returns((IEnumerable<ConfigResource> resources, DescribeConfigsOptions _) => Task.FromResult(describe(resources)));
        return admin.Object;
    }

    private async Task AssertDecommissionRefusedWhenRolesAreUnknown()
    {
        var (service, broker, _) = NewService(requireSecondApprover: true);
        broker.RolesKnown = true;
        broker.Eligible.Clear();
        broker.Eligible.Add(0);
        broker.Placements.Add(new PartitionPlacement
        {
            Topic = "ReadyToAcquire",
            Partition = 0,
            Leader = 1,
            Replicas = [1, 2, 3],
            Isr = [1, 2, 3]
        });
        var created = await service.CreateDecommissionAsync(User("alice", ManageScaling), 3, "remove the spare broker", null, CancellationToken.None);
        await service.ApproveAsync(User("bob", ManageScaling), created.Id, CancellationToken.None);
        broker.RolesKnown = false;

        var execute = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.ExecuteAsync(User("carol", ManageScaling), created.Id, CancellationToken.None));
        Assert.Contains("could not be read", execute.Message, StringComparison.Ordinal);
        Assert.Equal(KafkaChangeStatus.Failed, (await service.GetAsync(created.Id, CancellationToken.None))!.Status);

        var plan = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.PlanDecommissionAsync(3, CancellationToken.None));
        Assert.Contains("could not be read", plan.Message, StringComparison.Ordinal);
        var create = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            service.CreateDecommissionAsync(User("alice", ManageScaling), 3, "remove the spare broker", null, CancellationToken.None));
        Assert.Contains("could not be read", create.Message, StringComparison.Ordinal);
    }

    private const string ManageTopics = nameof(LinkSystemPermissions.CanManageKafkaTopics);
    private const string ManageScaling = nameof(LinkSystemPermissions.CanManageScaling);

    private static Mock<IAdminClient> GroupAdmin(Error offsetError)
    {
        var admin = new Mock<IAdminClient>();
        admin.Setup(client => client.DescribeConsumerGroupsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DescribeConsumerGroupsOptions>()))
            .ReturnsAsync(new DescribeConsumerGroupsResult
            {
                ConsumerGroupDescriptions = [new ConsumerGroupDescription { GroupId = "Report", Error = new Error(ErrorCode.NoError) }]
            });
        admin.Setup(client => client.ListConsumerGroupOffsetsAsync(It.IsAny<IEnumerable<ConsumerGroupTopicPartitions>>(), It.IsAny<ListConsumerGroupOffsetsOptions>()))
            .ReturnsAsync(
            [
                new ListConsumerGroupOffsetsResult
                {
                    Group = "Report",
                    Partitions = [new TopicPartitionOffsetError(new TopicPartition("ReadyToAcquire", 0), Offset.Unset, offsetError)]
                }
            ]);
        return admin;
    }

    private static async Task<int> Invoke(KafkaOpsEndpoints endpoints, string method, params object?[] args)
    {
        var found = typeof(KafkaOpsEndpoints).GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(found);
        var task = (Task<IResult>)found!.Invoke(endpoints, args)!;
        var result = await task;
        return (result as IStatusCodeHttpResult)?.StatusCode ?? 0;
    }

    private static ClaimsPrincipal Operator()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "pat"),
                new Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, ManageTopics),
                new Claim(LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions, ManageScaling)
            ],
            "test",
            ClaimTypes.Name,
            ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    private static (KafkaOpsService Service, FakeBroker Broker, FlipInfra Infra) NewService(bool requireSecondApprover, int scaleTimeoutSeconds = 180, bool production = false)
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
        var host = new TestHost();
        if (production)
            host.EnvironmentName = "Production";
        var service = new KafkaOpsService(
            broker,
            new SameReferenceCache(),
            options,
            host,
            NullLogger<KafkaOpsService>.Instance,
            configuration,
            Array.Empty<IProducer<string, AuditEventMessage>>(),
            infra);
        return (service, broker, infra);
    }

    private static async Task<int> StatusAsync(KafkaOpsEndpoints endpoints, string method, ClaimsPrincipal user, Guid id, object? body)
    {
        var found = typeof(KafkaOpsEndpoints).GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(found);
        var args = found!.GetParameters().Length == 4
            ? new object?[] { user, id, body, CancellationToken.None }
            : new object?[] { user, id, CancellationToken.None };
        var task = (Task<IResult>)found.Invoke(endpoints, args)!;
        var result = await task;
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        return status.StatusCode ?? 0;
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
        public string Name { get; set; } = "Test";
        public string Detail => "Infrastructure is disabled for this test.";
        public int CancelCalls { get; private set; }
        public int ReleaseCalls { get; private set; }
        public string LastRebalanceName { get; private set; } = "";

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

        public Task ApplyReassignmentAsync(string reassignmentJson, string rebalanceName, bool refresh, CancellationToken cancellationToken)
        {
            LastRebalanceName = rebalanceName;
            return Task.CompletedTask;
        }

        public Task CancelReassignmentAsync(string rebalanceName, CancellationToken cancellationToken)
        {
            CancelCalls++;
            LastRebalanceName = rebalanceName;
            return Task.CompletedTask;
        }

        public Task ReleaseRebalanceAsync(string rebalanceName, CancellationToken cancellationToken)
        {
            ReleaseCalls++;
            return Task.CompletedTask;
        }

        public ReassignmentListing Reassignments { get; set; } = new() { Known = true };

        public Task<ReassignmentListing> ListInFlightReassignmentsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Reassignments);
    }

    private sealed class FakeBroker : IKafkaBrokerGateway
    {
        public Exception? GroupError { get; set; }
        public int GroupCalls { get; private set; }
        public int CapabilityProbes { get; private set; }
        public List<GroupView> Groups { get; } = [];
        public List<(string Topic, int Count)> Increases { get; } = [];
        public string? FailIncreaseTopic { get; set; }
        public List<PartitionPlacement> Placements { get; } = [];
        public ReassignmentListing Reassignments { get; set; } = new() { Known = true };
        public List<ElectionType> Elections { get; } = [];
        public Exception? ElectionError { get; set; }
        public void SetCount(string topic, int count) => _counts[topic] = count;
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

        public Task<ReassignmentListing> ListInFlightReassignmentsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Reassignments);

        public Task ElectPreferredLeadersAsync(IReadOnlyList<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Elections.Add(KafkaLeaderElection.Kind);
            if (ElectionError is not null)
                throw ElectionError;
            return Task.CompletedTask;
        }

        public Task IncreasePartitionsAsync(string topic, int newCount, CancellationToken cancellationToken)
        {
            if (string.Equals(topic, FailIncreaseTopic, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The sibling increase failed.");
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
