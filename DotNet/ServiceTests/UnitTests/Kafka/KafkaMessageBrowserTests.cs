using System.Reflection;
using Task = System.Threading.Tasks.Task;
using Claim = System.Security.Claims.Claim;
using System.Security.Claims;
using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace UnitTests.Kafka;

public class KafkaMessageBrowserTests
{
    [Fact]
    public async Task Read_SeeksOldestNewestFromOffsetAndSince()
    {
        var sessions = new FakeSessions();
        sessions.Session.Partitions.Add(Partition(0, 0, 10));
        sessions.Session.Partitions.Add(Partition(1, 2, 20));
        var browser = Browser(sessions);

        await browser.ReadAsync(Request("oldest", limit: 2), CancellationToken.None);
        Assert.Equal(2, sessions.Session.Seeks.Count);
        Assert.Equal(0, sessions.Session.Seeks[0].Offset);
        Assert.Equal(2, sessions.Session.Seeks[1].Offset);

        sessions.Session.Seeks.Clear();
        sessions.Session.Queue.Add(Polled(0, 8, 100, "a"));
        sessions.Session.Queue.Add(Polled(0, 9, 300, "b"));
        sessions.Session.Queue.Add(End(0));
        sessions.Session.Queue.Add(Polled(1, 18, 200, "c"));
        sessions.Session.Queue.Add(End(1));
        var newest = await browser.ReadAsync(Request("newest", limit: 2), CancellationToken.None);
        Assert.Equal(10 - 2, sessions.Session.Seeks.First(seek => seek.Partition == 0).Offset);
        Assert.Equal(20 - 2, sessions.Session.Seeks.First(seek => seek.Partition == 1).Offset);
        Assert.Equal(new long[] { 9, 18 }, newest.Records.Select(record => record.Offset).ToArray());

        sessions.Session.Seeks.Clear();
        sessions.Session.Queue.Clear();
        await browser.ReadAsync(Request("from-offset", limit: 1, offset: 4), CancellationToken.None);
        Assert.All(sessions.Session.Seeks, seek => Assert.Equal(4, seek.Offset));

        sessions.Session.Seeks.Clear();
        await browser.ReadAsync(Request("from-offset", limit: 1, offset: -1), CancellationToken.None);
        Assert.Equal(0, sessions.Session.Seeks.First(seek => seek.Partition == 0).Offset);
        Assert.Equal(2, sessions.Session.Seeks.First(seek => seek.Partition == 1).Offset);

        sessions.Session.Seeks.Clear();
        sessions.Session.TimeOffset = 6;
        await browser.ReadAsync(Request("since", limit: 1, timestamp: 1_700_000_000_000), CancellationToken.None);
        Assert.Contains(sessions.Session.TimeLookups, lookup => lookup.UnixMs == 1_700_000_000_000);
        Assert.All(sessions.Session.Seeks, seek => Assert.Equal(6, seek.Offset));
    }

    [Fact]
    public async Task Read_FiltersPartitionsAndKeySubstring()
    {
        var sessions = new FakeSessions();
        sessions.Session.Partitions.Add(Partition(0, 0, 5));
        sessions.Session.Partitions.Add(Partition(1, 0, 5));
        sessions.Session.Queue.Add(Polled(1, 0, 1, "alpha-1"));
        sessions.Session.Queue.Add(Polled(1, 1, 2, "beta"));
        sessions.Session.Queue.Add(End(1));
        var browser = Browser(sessions);

        var page = await browser.ReadAsync(Request("oldest", limit: 10, partitions: [1], key: "alpha"), CancellationToken.None);

        Assert.Single(sessions.Session.Seeks);
        Assert.Equal(1, sessions.Session.Seeks[0].Partition);
        Assert.Single(page.Records);
        Assert.Equal("alpha-1", page.Records[0].Key);
    }

    [Fact]
    public async Task Read_RefusesAMissingOffsetOrTimestampBeforeOpeningASession()
    {
        var sessions = new FakeSessions();
        var browser = Browser(sessions);

        var offset = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            browser.ReadAsync(Request("from-offset", limit: 1), CancellationToken.None));
        var timestamp = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            browser.ReadAsync(Request("since", limit: 1), CancellationToken.None));

        Assert.Contains("offset", offset.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("timestamp", timestamp.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, sessions.Opens);
    }

    [Fact]
    public async Task Read_EnforcesTheLimitTheByteBudgetAndTheScanCap()
    {
        var sessions = new FakeSessions();
        var browser = Browser(sessions);
        var over = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            browser.ReadAsync(Request("newest", limit: 51), CancellationToken.None));
        var zero = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            browser.ReadAsync(Request("newest", limit: 0), CancellationToken.None));
        Assert.Contains("1 to 50", over.Message, StringComparison.Ordinal);
        Assert.Contains("1 to 50", zero.Message, StringComparison.Ordinal);
        Assert.Equal(0, sessions.Opens);

        sessions.Session.Partitions.Add(Partition(0, 0, 3));
        var accepted = await browser.ReadAsync(Request("oldest", limit: 50), CancellationToken.None);
        Assert.Equal(0, accepted.Metadata.Returned);
        Assert.False(accepted.Metadata.CapHit);

        sessions.Session.Queue.Add(Polled(0, 0, 1, "keep", new byte[8]));
        sessions.Session.Queue.Add(End(0));
        var capped = await browser.ReadAsync(Request("oldest", limit: 10, byteBudget: 4), CancellationToken.None);
        Assert.True(capped.Metadata.CapHit);
        Assert.True(capped.Metadata.Truncated);
        Assert.Single(capped.Records);
        Assert.True(capped.Records[0].Truncated);

        sessions.Session.Queue.Clear();
        for (var i = 0; i < KafkaBrowseLimits.MaxScanned + 1; i++)
            sessions.Session.Queue.Add(Polled(0, i, i, "nope", [1]));
        sessions.Session.Partitions[0] = Partition(0, 0, KafkaBrowseLimits.MaxScanned + 5);
        var scanned = await browser.ReadAsync(Request("oldest", limit: 25, key: "zzzz"), CancellationToken.None);
        Assert.True(scanned.Metadata.CapHit);
        Assert.Empty(scanned.Records);
        Assert.True(sessions.Session.Disposed);
    }

    [Fact]
    public async Task Read_StopsWhenCancelledAndDoesNotOpenASession()
    {
        var sessions = new FakeSessions();
        var browser = Browser(sessions);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            browser.ReadAsync(Request("newest", limit: 1), new CancellationToken(canceled: true)));

        Assert.Equal(0, sessions.Opens);
        Assert.Empty(sessions.Session.Seeks);
    }

    [Fact]
    public void BrowseSession_HasNoCommitAndDisablesAutoCommit()
    {
        Assert.DoesNotContain(typeof(IKafkaBrowseSession).GetMethods().Select(method => method.Name), Commits);
        Assert.DoesNotContain(
            typeof(ConfluentBrowseSession).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).Select(method => method.Name),
            Commits);

        var config = ConfluentBrowseSession.BrowseConfig(new KafkaConnection { BootstrapServers = ["localhost:9092"] });
        Assert.False(config.EnableAutoCommit);
        Assert.False(config.EnableAutoOffsetStore);
        Assert.Equal(KafkaBrowseLimits.GroupId, config.GroupId);
        Assert.Equal(AutoOffsetReset.Error, config.AutoOffsetReset);
        Assert.False(config.AllowAutoCreateTopics);
    }

    [Fact]
    public void AssignAndSeek_AssignsTopicPartitionOffsetsInOneStep()
    {
        var consumer = new Mock<IConsumer<byte[], byte[]>>();
        List<TopicPartitionOffset>? assigned = null;
        consumer
            .Setup(item => item.Assign(It.IsAny<IEnumerable<TopicPartitionOffset>>()))
            .Callback<IEnumerable<TopicPartitionOffset>>(offsets => assigned = offsets.ToList());

        var session = new ConfluentBrowseSession(consumer.Object);
        session.AssignAndSeek(
        [
            new KafkaBrowseSeek { Topic = "ResourcesAcquired", Partition = 0, Offset = 12 },
            new KafkaBrowseSeek { Topic = "ResourcesAcquired", Partition = 3, Offset = 40 }
        ]);

        Assert.NotNull(assigned);
        Assert.Equal(2, assigned!.Count);
        Assert.Equal("ResourcesAcquired", assigned[0].Topic);
        Assert.Equal(0, assigned[0].Partition.Value);
        Assert.Equal(12, assigned[0].Offset.Value);
        Assert.Equal(3, assigned[1].Partition.Value);
        Assert.Equal(40, assigned[1].Offset.Value);
        consumer.Verify(item => item.Assign(It.IsAny<IEnumerable<TopicPartitionOffset>>()), Times.Once);
        consumer.Verify(item => item.Assign(It.IsAny<IEnumerable<TopicPartition>>()), Times.Never);
        consumer.Verify(item => item.Seek(It.IsAny<TopicPartitionOffset>()), Times.Never);
    }

    [Fact]
    public void AssignAndSeek_DoesNotSeekBeforeTheAssignmentIsApplied()
    {
        var config = ConfluentBrowseSession.BrowseConfig(new KafkaConnection
        {
            BootstrapServers = ["127.0.0.1:1"]
        });
        config.SocketTimeoutMs = 200;
        config.ReconnectBackoffMs = 10_000;
        config.ReconnectBackoffMaxMs = 10_000;

        var consumer = new ConsumerBuilder<byte[], byte[]>(config)
            .SetErrorHandler((_, _) => { })
            .SetLogHandler((_, _) => { })
            .Build();
        var session = new ConfluentBrowseSession(consumer);
        try
        {
            var thrown = Record.Exception(() => session.AssignAndSeek(
            [
                new KafkaBrowseSeek { Topic = "ResourcesAcquired", Partition = 0, Offset = 12 },
                new KafkaBrowseSeek { Topic = "ResourcesAcquired", Partition = 1, Offset = 4 }
            ]));

            Assert.Null(thrown);
            Assert.Equal(2, consumer.Assignment.Count);
            Assert.Contains(consumer.Assignment, partition => partition.Partition.Value == 0);
            Assert.Contains(consumer.Assignment, partition => partition.Partition.Value == 1);
        }
        finally
        {
            session.Dispose();
        }
    }

    [Fact]
    public async Task Export_WritesAnAuditAndStillDoesNotCommit()
    {
        var sessions = new FakeSessions();
        sessions.Session.Partitions.Add(Partition(0, 0, 5));
        sessions.Session.Queue.Add(Polled(0, 1, 1_710_000_000_000, "{\"facilityId\":\"fac-1\"}", Encoding.UTF8.GetBytes("{\"reportId\":\"rep-1\"}")));
        sessions.Session.Queue.Add(End(0));
        Message<string, AuditEventMessage>? sent = null;
        var producer = new Mock<IProducer<string, AuditEventMessage>>();
        producer.Setup(item => item.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, AuditEventMessage>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, AuditEventMessage>, CancellationToken>((_, message, _) => sent = message)
            .ReturnsAsync(new DeliveryResult<string, AuditEventMessage>());
        var browser = Browser(sessions, [producer.Object]);
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "pat")], "test"));

        var page = await browser.ExportAsync(user, Request("oldest", limit: 5), CancellationToken.None);

        Assert.Equal("fac-1", page.Records[0].Link.FacilityId);
        Assert.Equal("rep-1", page.Records[0].Link.ReportId);
        Assert.NotNull(sent);
        Assert.Equal(nameof(KafkaTopic.AuditableEventOccurred), producer.Invocations[0].Arguments[0]);
        Assert.Equal(AuditEventType.Query, sent!.Value.Action);
        Assert.Equal("ResourcesAcquired", sent.Value.Resource);
        Assert.Equal("pat", sent.Value.User);
        Assert.True(sessions.Session.Disposed);
        Assert.DoesNotContain(typeof(IKafkaBrowseSession).GetMethods().Select(method => method.Name), Commits);
    }

    [Fact]
    public async Task Family_ReportsExistsPartitionsHighWatermarkAndGroupLag()
    {
        var gateway = new Mock<IKafkaBrokerGateway>();
        gateway.Setup(item => item.DescribeTopicsAsync(It.IsAny<IReadOnlyList<string>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> topics, bool _, CancellationToken _) =>
                (IReadOnlyList<TopicWatermark>)topics.Select(name => name is "ResourcesAcquired" or "ResourcesAcquired-Error"
                    ? new TopicWatermark
                    {
                        Topic = name,
                        Partitions = 3,
                        HighWatermarks = name == "ResourcesAcquired" ? [10, 20, 30] : [1, 1, 1]
                    }
                    : new TopicWatermark { Topic = name, Error = "Unknown topic or partition" }).ToList());
        gateway.Setup(item => item.DescribeGroupsAsync(false, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new GroupView
                {
                    GroupId = "Normalization",
                    Partitions =
                    [
                        new PartitionLagView { Topic = "ResourcesAcquired", Partition = 0, Lag = 4 },
                        new PartitionLagView { Topic = "ResourcesAcquired", Partition = 1, Lag = 6 },
                        new PartitionLagView { Topic = "ResourcesAcquired-Error", Partition = 0, Lag = 2 },
                        new PartitionLagView { Topic = "Other", Partition = 0, Lag = 99 }
                    ]
                }
            ]);
        var sessions = new FakeSessions();
        var browser = Browser(sessions, gateway: gateway.Object);

        var family = await browser.FamilyAsync("ResourcesAcquired-Error", CancellationToken.None);

        Assert.Equal("ResourcesAcquired", family.Main);
        var main = family.Members.Single(member => member.Kind == KafkaBrowseAllowList.KindMain);
        var error = family.Members.Single(member => member.Kind == KafkaBrowseAllowList.KindError);
        var retry = family.Members.Single(member => member.Topic == "ResourcesAcquired-Retry-Normalization");
        Assert.True(main.Exists);
        Assert.Equal(3, main.Partitions);
        Assert.Equal(60, main.HighWatermarkSum);
        Assert.Equal(10, main.Lag);
        Assert.True(error.Exists);
        Assert.Equal(3, error.HighWatermarkSum);
        Assert.Equal(2, error.Lag);
        Assert.False(retry.Exists);
        Assert.Equal(0, sessions.Opens);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            browser.FamilyAsync("ResourcesAcquired", new CancellationToken(canceled: true)));
    }

    [Theory]
    [InlineData(false, false, StatusCodes.Status401Unauthorized)]
    [InlineData(true, false, StatusCodes.Status403Forbidden)]
    public async Task Messages_RequiresAuthenticationAndInfrastructureView(bool authenticated, bool canView, int status)
    {
        var ops = Ops(canView);
        var browser = new Mock<IKafkaMessageBrowser>();
        var endpoints = new KafkaOpsEndpoints(ops.Object, NullLogger<KafkaOpsEndpoints>.Instance, null, browser.Object);
        var user = authenticated
            ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "pat")], "test"))
            : new ClaimsPrincipal(new ClaimsIdentity());

        var result = await Call(endpoints, "GetMessages", user, "ResourcesAcquired", null, null, null, null, null, null, null, null, CancellationToken.None);

        Assert.Equal(status, await Status(result));
        browser.Verify(item => item.ReadAsync(It.IsAny<KafkaBrowseRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Messages_ReturnsThePageAndRefusesABadLimitOrUnknownTopic()
    {
        var sessions = new FakeSessions();
        sessions.Session.Partitions.Add(Partition(0, 0, 4));
        sessions.Session.Queue.Add(Polled(0, 3, 1_710_000_000_000, "fac-9|pat-9", Encoding.UTF8.GetBytes("{\"reportId\":\"rep-9\"}")));
        sessions.Session.Queue.Add(End(0));
        var endpoints = new KafkaOpsEndpoints(Ops(true).Object, NullLogger<KafkaOpsEndpoints>.Instance, null, Browser(sessions));

        var ok = await Call(endpoints, "GetMessages", Viewer(), "ResourcesAcquired", "newest", null, null, null, "fac<script>", null, null, new[] { 0 }, CancellationToken.None);
        Assert.Equal(StatusCodes.Status200OK, await Status(ok));

        var limit = await Call(endpoints, "GetMessages", Viewer(), "ResourcesAcquired", "oldest", null, null, (int?)0, null, null, null, null, CancellationToken.None);
        Assert.Equal(StatusCodes.Status400BadRequest, await Status(limit));

        var unknown = await Call(endpoints, "GetMessages", Viewer(), "NotAPipelineTopic", "newest", null, null, null, null, null, null, null, CancellationToken.None);
        Assert.Equal(StatusCodes.Status400BadRequest, await Status(unknown));
    }

    [Fact]
    public async Task Messages_MapsBusyTo409BrokerTo502AndAMissingBrowserTo503()
    {
        var busyBrowser = new Mock<IKafkaMessageBrowser>();
        busyBrowser.Setup(item => item.ReadAsync(It.IsAny<KafkaBrowseRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KafkaBrowseBusyException());
        var busy = new KafkaOpsEndpoints(Ops(true).Object, NullLogger<KafkaOpsEndpoints>.Instance, null, busyBrowser.Object);
        Assert.Equal(StatusCodes.Status409Conflict, await Status(await Call(busy, "GetMessages", Viewer(), "ResourcesAcquired", "newest", null, null, null, null, null, null, null, CancellationToken.None)));

        var brokerBrowser = new Mock<IKafkaMessageBrowser>();
        brokerBrowser.Setup(item => item.ReadAsync(It.IsAny<KafkaBrowseRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KafkaBrowseBrokerException("The broker refused the read."));
        var broker = new KafkaOpsEndpoints(Ops(true).Object, NullLogger<KafkaOpsEndpoints>.Instance, null, brokerBrowser.Object);
        Assert.Equal(StatusCodes.Status502BadGateway, await Status(await Call(broker, "GetMessages", Viewer(), "ResourcesAcquired", "newest", null, null, null, null, null, null, null, CancellationToken.None)));

        var missing = new KafkaOpsEndpoints(Ops(true).Object, NullLogger<KafkaOpsEndpoints>.Instance);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, await Status(await Call(missing, "GetMessages", Viewer(), "ResourcesAcquired", "newest", null, null, null, null, null, null, null, CancellationToken.None)));
    }

    [Fact]
    public async Task Export_ReturnsTheShownRowsAndFamilyUsesTheSameViewGate()
    {
        var sessions = new FakeSessions();
        sessions.Session.Partitions.Add(Partition(0, 0, 4));
        sessions.Session.Queue.Add(Polled(0, 3, 1_710_000_000_000, "fac-9", Encoding.UTF8.GetBytes("{\"patientId\":\"pat-9\"}")));
        sessions.Session.Queue.Add(End(0));
        var producer = new Mock<IProducer<string, AuditEventMessage>>();
        producer.Setup(item => item.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, AuditEventMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, AuditEventMessage>());
        var endpoints = new KafkaOpsEndpoints(Ops(true).Object, NullLogger<KafkaOpsEndpoints>.Instance, null, Browser(sessions, [producer.Object]));

        var exported = await Call(endpoints, "ExportMessages", Viewer(), "ResourcesAcquired", "oldest", null, null, (int?)25, null, null, null, null, CancellationToken.None);
        var file = Assert.IsAssignableFrom<FileContentHttpResult>(exported);
        Assert.Contains("application/json", file.ContentType ?? "", StringComparison.Ordinal);
        Assert.Equal("ResourcesAcquired-messages.json", file.FileDownloadName);
        var json = Encoding.UTF8.GetString(file.FileContents.Span);
        Assert.Contains("\"partition\": 0", json, StringComparison.Ordinal);
        Assert.Contains("fac-9", json, StringComparison.Ordinal);
        producer.Verify(item => item.ProduceAsync(nameof(KafkaTopic.AuditableEventOccurred), It.IsAny<Message<string, AuditEventMessage>>(), It.IsAny<CancellationToken>()), Times.Once);

        var denied = await Call(endpoints, "GetBrowseFamily", new ClaimsPrincipal(new ClaimsIdentity()), "ResourcesAcquired", CancellationToken.None);
        Assert.Equal(StatusCodes.Status401Unauthorized, await Status(denied));
    }

    [Fact]
    public void Routes_RequireInfrastructureView()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        string? path = null;
        while (root is not null && path is null)
        {
            var candidate = Path.Combine(root.FullName, "DotNet", "Admin.BFF", "Presentation", "Endpoints", "KafkaOpsEndpoints.cs");
            if (File.Exists(candidate))
                path = candidate;
            root = root.Parent;
        }

        Assert.NotNull(path);
        var text = File.ReadAllText(path!);
        Assert.Contains("MapGet(\"/topics/{topic}/messages\"", text, StringComparison.Ordinal);
        Assert.Contains("MapGet(\"/topics/{topic}/messages/export\"", text, StringComparison.Ordinal);
        Assert.Contains("MapGet(\"/topics/{topic}/family\"", text, StringComparison.Ordinal);
        Assert.Contains("RequireAuthorization(PolicyNames.CanViewInfrastructure)", text, StringComparison.Ordinal);
        Assert.Contains(nameof(PolicyNames.CanViewInfrastructure), text, StringComparison.Ordinal);
    }

    private static bool Commits(string name) =>
        name.Contains("Commit", StringComparison.Ordinal) || name.Contains("StoreOffset", StringComparison.Ordinal);

    private static KafkaMessageBrowser Browser(FakeSessions sessions, IEnumerable<IProducer<string, AuditEventMessage>>? producers = null, IKafkaBrokerGateway? gateway = null) =>
        new(
            sessions,
            gateway ?? new Mock<IKafkaBrokerGateway>().Object,
            NullLogger<KafkaMessageBrowser>.Instance,
            producers ?? Array.Empty<IProducer<string, AuditEventMessage>>());

    private static Mock<IKafkaOpsService> Ops(bool canView)
    {
        var ops = new Mock<IKafkaOpsService>();
        ops.Setup(service => service.CanView(It.IsAny<ClaimsPrincipal>()))
            .Returns((ClaimsPrincipal user) => canView && user.Identity?.IsAuthenticated == true);
        return ops;
    }

    private static ClaimsPrincipal Viewer() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "pat")], "test"));

    private static KafkaBrowseRequest Request(string mode, int limit, long? offset = null, long? timestamp = null, int[]? partitions = null, string key = "", int? byteBudget = null) =>
        new()
        {
            Topic = "ResourcesAcquired",
            Mode = mode,
            Limit = limit,
            Offset = offset,
            TimestampUnixMs = timestamp,
            Partitions = partitions ?? [],
            Key = key,
            ByteBudget = byteBudget
        };

    private static KafkaBrowsePartition Partition(int id, long low, long high) =>
        new() { Id = id, Low = low, High = high };

    private static KafkaBrowsePolled Polled(int partition, long offset, long timestamp, string key, byte[]? value = null) =>
        new()
        {
            Partition = partition,
            Offset = offset,
            TimestampUnixMs = timestamp,
            Key = Encoding.UTF8.GetBytes(key),
            Value = value ?? Encoding.UTF8.GetBytes("{\"ok\":true}")
        };

    private static KafkaBrowsePolled End(int partition) =>
        new() { Partition = partition, EndOfPartition = true };

    private static async Task<IResult> Call(KafkaOpsEndpoints endpoints, string method, params object?[] args)
    {
        var found = typeof(KafkaOpsEndpoints).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(found);
        var task = (Task<IResult>)found!.Invoke(endpoints, args)!;
        return await task;
    }

    private static async Task<int> Status(IResult result)
    {
        if (result is IStatusCodeHttpResult coded && coded.StatusCode is int status)
            return status;
        // ForbidHttpResult resolves the authentication service only when it executes.
        if (result is ForbidHttpResult)
            return StatusCodes.Status403Forbidden;
        var http = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        await result.ExecuteAsync(http);
        return http.Response.StatusCode;
    }

    private sealed class FakeSessions : IKafkaBrowseSessionFactory
    {
        public FakeBrowse Session { get; } = new();
        public int Opens { get; private set; }

        public IKafkaBrowseSession Open()
        {
            Opens++;
            Session.Disposed = false;
            return Session;
        }
    }

    private sealed class FakeBrowse : IKafkaBrowseSession
    {
        public List<KafkaBrowsePartition> Partitions { get; } = [];
        public List<KafkaBrowsePolled> Queue { get; } = [];
        public List<KafkaBrowseSeek> Seeks { get; } = [];
        public List<(string Topic, int Partition, long UnixMs)> TimeLookups { get; } = [];
        public long? TimeOffset { get; set; } = 6;
        public bool Disposed { get; set; }

        public IReadOnlyList<KafkaBrowsePartition> Describe(string topic) => Partitions;

        public long? OffsetForTime(string topic, int partition, long unixMs)
        {
            TimeLookups.Add((topic, partition, unixMs));
            return TimeOffset;
        }

        public void AssignAndSeek(IReadOnlyList<KafkaBrowseSeek> seeks) => Seeks.AddRange(seeks);

        public KafkaBrowsePolled? Poll(TimeSpan wait)
        {
            if (Queue.Count == 0)
                return null;
            var next = Queue[0];
            Queue.RemoveAt(0);
            return next;
        }

        public void Dispose() => Disposed = true;
    }
}
