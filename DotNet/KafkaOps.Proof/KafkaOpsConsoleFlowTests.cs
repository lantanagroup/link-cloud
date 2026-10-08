using System.Security.Claims;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Link.Authorization.Infrastructure;
using Link.Authorization.Permissions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KafkaOps.Proof;

public class KafkaOpsConsoleFlowTests
{
    [Fact]
    public async Task TwoPeopleApproveAndExecute_AndThreeGroupsAreListed()
    {
        var bootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP");
        if (string.IsNullOrWhiteSpace(bootstrap))
            return;

        var catalogTopics = new[]
        {
            "ReadyToAcquire",
            KafkaTopicCatalog.RetryName("ReadyToAcquire"),
            KafkaTopicCatalog.ErrorName("ReadyToAcquire")
        };
        const string memberTopic = "ops-proof-members";
        var groups = new[] { "ops-proof-g1", "ops-proof-g2", "ops-proof-g3" };
        var consumers = new List<IConsumer<string, string>>();

        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrap }).Build();
        var overview = admin.GetMetadata(TimeSpan.FromSeconds(20));
        Assert.True(overview.Brokers.Count >= 3, "The proof cluster needs three brokers.");

        try
        {
            await admin.DeleteTopicsAsync(
                catalogTopics.Append(memberTopic).ToList(),
                new DeleteTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(20) });
        }
        catch (KafkaException)
        {
            // The topics are absent on a fresh cluster.
        }

        await admin.CreateTopicsAsync(
            catalogTopics.Append(memberTopic).Select(name => new TopicSpecification
            {
                Name = name,
                NumPartitions = 3,
                ReplicationFactor = 3
            }).ToList(),
            new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(30) });

        using (var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrap,
            AllowAutoCreateTopics = false
        }).SetKeySerializer(Serializers.Utf8).SetValueSerializer(Serializers.Utf8).Build())
        {
            producer.Produce(memberTopic, new Message<string, string> { Key = "proof", Value = "member" });
            producer.Flush(TimeSpan.FromSeconds(15));
        }

        try
        {
            foreach (var group in groups)
            {
                var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
                {
                    BootstrapServers = bootstrap,
                    GroupId = group,
                    ClientId = group + "-reader",
                    EnableAutoCommit = false,
                    AllowAutoCreateTopics = false,
                    AutoOffsetReset = AutoOffsetReset.Earliest
                }).SetKeyDeserializer(Deserializers.Utf8).SetValueDeserializer(Deserializers.Utf8).Build();
                consumer.Subscribe(memberTopic);
                consumers.Add(consumer);
            }

            var until = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < until)
            {
                foreach (var consumer in consumers)
                    consumer.Consume(TimeSpan.FromMilliseconds(200));
            }

            using var gateway = new KafkaBrokerGateway(new KafkaConnection
            {
                BootstrapServers = [bootstrap],
                SaslProtocolEnabled = false
            });
            var described = await gateway.DescribeGroupsAsync(false, 1, CancellationToken.None);
            foreach (var group in groups)
                Assert.Contains(described, row => row.GroupId == group);

            var service = new KafkaOpsService(
                gateway,
                new FlowCache(),
                Options.Create(new KafkaOpsOptions
                {
                    RequireSecondApprover = true,
                    MaxPartitionsPerTopic = 24,
                    RateLimitMinutes = 30,
                    CacheSeconds = 8
                }),
                new FlowHost(),
                NullLogger<KafkaOpsService>.Instance,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:EnableAnonymousAccess"] = "false"
                }).Build(),
                Array.Empty<IProducer<string, AuditEventMessage>>(),
                new DisabledKafkaInfraProvider("Infrastructure is disabled for this test."));

            var created = await service.CreateAsync(
                User("alice", nameof(LinkSystemPermissions.CanManageKafkaTopics)),
                "ReadyToAcquire",
                4,
                "raise the log topic",
                false,
                null,
                "ReadyToAcquire",
                null,
                CancellationToken.None);
            await service.ApproveAsync(
                User("bob", nameof(LinkSystemPermissions.CanManageKafkaTopics)),
                created.Id,
                CancellationToken.None);
            var executed = await service.ExecuteAsync(
                User("carol", nameof(LinkSystemPermissions.CanManageKafkaTopics)),
                created.Id,
                CancellationToken.None);
            Assert.Equal(KafkaChangeStatus.Converging, executed.Status);

            foreach (var topic in catalogTopics)
            {
                var metadata = admin.GetMetadata(topic, TimeSpan.FromSeconds(20));
                var describedTopic = metadata.Topics.Single(item => item.Topic == topic);
                Assert.Equal(4, describedTopic.Partitions.Count);
            }
        }
        finally
        {
            foreach (var consumer in consumers)
            {
                consumer.Close();
                consumer.Dispose();
            }
        }
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

    private sealed class FlowHost : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "proof";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FlowCache : LantanaGroup.Link.Shared.Application.Interfaces.ICacheService
    {
        private readonly Dictionary<string, object> _store = new(StringComparer.Ordinal);

        public Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (_store.TryGetValue(key, out var value) && value is T typed)
                return Task.FromResult(typed);
            return Task.FromResult(default(T)!);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan expiration, LantanaGroup.Link.Shared.Application.Models.Configs.ExpirationType expirationType = LantanaGroup.Link.Shared.Application.Models.Configs.ExpirationType.Sliding, CancellationToken cancellationToken = default)
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
}
