using System.Text;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;
using LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps.Protocol;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Moq;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Kafka;

public class KafkaAdminProtocolTests
{
    [Fact]
    public void Scram_MatchesTheRfc7677Sha256Vector()
    {
        var scram = new KafkaScram("SCRAM-SHA-256", "user", "pencil", "rOprNGfwEbeRWgbNEkqO");
        Assert.Equal("n,,n=user,r=rOprNGfwEbeRWgbNEkqO", Encoding.UTF8.GetString(scram.ClientFirst()));
        var final = scram.ClientFinal(Encoding.UTF8.GetBytes("r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096"));
        Assert.Equal("c=biws,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,p=dHzbZapWIk4jUhN+Ute9ytag9zjfMHgsqmmiz7AndVQ=", Encoding.UTF8.GetString(final));
        scram.VerifyServerFinal(Encoding.UTF8.GetBytes("v=6rriTRBi23WpRR/wtup+mMhUZUn/dB5nLTJRsjl95G4="));
        Assert.Throws<KafkaWireException>(() => scram.VerifyServerFinal(Encoding.UTF8.GetBytes("v=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")));
    }

    [Fact]
    public void Scram_RefusesANonceTheClientDidNotStart()
    {
        var scram = new KafkaScram("SCRAM-SHA-512", "user", "pencil", "abc");
        scram.ClientFirst();
        Assert.Throws<KafkaWireException>(() => scram.ClientFinal(Encoding.UTF8.GetBytes("r=zzz123,s=AAAA,i=4096")));
    }

    [Fact]
    public void AlterRequest_GroupsByTopic_AndEncodesCancelAsNullReplicas()
    {
        var body = KafkaAdminMessages.AlterPartitionReassignmentsRequest(
            [new PartitionTarget("a", 0, [1, 2]), new PartitionTarget("a", 1, null), new PartitionTarget("b", 3, [3])], 30_000);
        var reader = new KafkaWireReader(body);
        Assert.Equal(30_000, reader.Int32());
        Assert.Equal(2, reader.CompactArrayLength());
        Assert.Equal("a", reader.CompactString());
        Assert.Equal(2, reader.CompactArrayLength());
        Assert.Equal(0, reader.Int32());
        Assert.Equal([1, 2], reader.CompactInt32Array());
        reader.SkipTags();
        Assert.Equal(1, reader.Int32());
        Assert.Equal(-1, reader.CompactArrayLength());
        reader.SkipTags();
        reader.SkipTags();
        Assert.Equal("b", reader.CompactString());
    }

    [Fact]
    public void ListResponse_ReadsOngoingMoves_AndRaisesTopLevelErrors()
    {
        var ok = new KafkaWireWriter().Int32(0).Int16(0).CompactString(null)
            .CompactArrayLength(1).CompactString("orders").CompactArrayLength(1)
            .Int32(7).CompactInt32Array([1, 2, 3]).CompactInt32Array([3]).CompactInt32Array([1]).NoTags()
            .NoTags().NoTags().ToArray();
        var ongoing = KafkaAdminMessages.ReadListPartitionReassignments(new KafkaWireReader(ok));
        var only = Assert.Single(ongoing);
        Assert.Equal(("orders", 7), (only.Topic, only.Partition));
        Assert.Equal([3], only.Adding);
        Assert.Equal([1], only.Removing);

        var refused = new KafkaWireWriter().Int32(0).Int16(KafkaAdminMessages.ErrorClusterAuthorizationFailed).CompactString("no").CompactArrayLength(0).NoTags().ToArray();
        var error = Assert.Throws<KafkaAdminProtocolException>(() => KafkaAdminMessages.ReadListPartitionReassignments(new KafkaWireReader(refused)));
        Assert.Equal(KafkaAdminMessages.ErrorClusterAuthorizationFailed, error.Code);
    }

    [Fact]
    public void Settings_UseTheSameKafkaConnectionShapeAsTheServices()
    {
        var scram = KafkaWireSettings.From(new KafkaConnection
        {
            BootstrapServers = ["kafka-kafka-bootstrap.kafka-dev:9092", "PLAINTEXT://other:9093/"],
            SaslProtocolEnabled = true,
            Protocol = Confluent.Kafka.SecurityProtocol.SaslPlaintext,
            Mechanism = Confluent.Kafka.SaslMechanism.ScramSha512,
            SaslUsername = "link-admin",
            SaslPassword = "x"
        });
        Assert.Equal([("kafka-kafka-bootstrap.kafka-dev", 9092), ("other", 9093)], scram.Servers);
        Assert.Equal("SCRAM-SHA-512", scram.SaslMechanism);
        Assert.False(scram.Tls);
        Assert.Equal("", scram.Problem);

        var plain = KafkaWireSettings.From(new KafkaConnection { BootstrapServers = ["localhost:9092"], SaslProtocolEnabled = false });
        Assert.Null(plain.SaslMechanism);

        var oauth = KafkaWireSettings.From(new KafkaConnection
        {
            BootstrapServers = ["localhost:9092"],
            SaslProtocolEnabled = true,
            Mechanism = Confluent.Kafka.SaslMechanism.OAuthBearer
        });
        Assert.NotEqual("", oauth.Problem);
    }

    [Fact]
    public void ThrottlePlan_CoversSendersAndReceivers_InKafkaToolFormat()
    {
        var plan = ReplicationThrottlePlan.For(
            [new PlannedMove { Topic = "t", Partition = 0, Replicas = [2, 3] }, new PlannedMove { Topic = "t", Partition = 1, Replicas = [1, 2] }],
            new Dictionary<(string Topic, int Partition), IReadOnlyList<int>> { [("t", 0)] = [1, 2], [("t", 1)] = [1, 2] });
        Assert.Equal([1, 2, 3], plan.Brokers);
        Assert.Equal("0:1,0:2", plan.Leaders["t"]);
        Assert.Equal("0:3", plan.Followers["t"]);
    }

    [Fact]
    public async Task Mover_SubmitsInWaves_AndCancelsOnlyWhatIsStillMoving()
    {
        var sent = new List<IReadOnlyList<PartitionTarget>>();
        var admin = new Mock<IKafkaReassignmentAdmin>();
        admin.Setup(item => item.AlterAsync(It.IsAny<IReadOnlyList<PartitionTarget>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<PartitionTarget> targets, CancellationToken _) =>
            {
                sent.Add(targets);
                var outcome = new ReassignmentOutcome();
                outcome.Partitions.AddRange(targets.Select(target => new PartitionOutcome { Topic = target.Topic, Partition = target.Partition }));
                return outcome;
            });
        admin.Setup(item => item.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OngoingReassignment { Topic = "t", Partition = 2 }, new OngoingReassignment { Topic = "other", Partition = 0 }]);
        var mover = new KafkaNativePartitionMover(admin.Object, Mock.Of<IKafkaBrokerGateway>(), Mock.Of<IKafkaThrottleConfig>(), 2);
        const string json = "{\"version\":1,\"partitions\":[{\"topic\":\"t\",\"partition\":0,\"replicas\":[1]},{\"topic\":\"t\",\"partition\":1,\"replicas\":[2]},{\"topic\":\"t\",\"partition\":2,\"replicas\":[3]}]}";

        Assert.Equal(2, await mover.SubmitAsync(json, 0, 0, CancellationToken.None));
        Assert.Equal(3, await mover.SubmitAsync(json, 2, 0, CancellationToken.None));
        Assert.Equal(3, await mover.SubmitAsync(json, 3, 0, CancellationToken.None));
        Assert.Equal([2, 1], sent.Select(wave => wave.Count));

        await mover.CancelAsync(json, CancellationToken.None);
        var cancel = Assert.Single(sent.Last());
        Assert.Equal(("t", 2), (cancel.Topic, cancel.Partition));
        Assert.Null(cancel.Replicas);
    }

    [Fact]
    public async Task Mover_ReportsThePartitionKafkaRefused()
    {
        var admin = new Mock<IKafkaReassignmentAdmin>();
        admin.Setup(item => item.AlterAsync(It.IsAny<IReadOnlyList<PartitionTarget>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var outcome = new ReassignmentOutcome();
                outcome.Partitions.Add(new PartitionOutcome { Topic = "t", Partition = 0, ErrorCode = KafkaAdminMessages.ErrorInvalidReplicaAssignment, ErrorMessage = "broker 9 is not alive" });
                return outcome;
            });
        var mover = new KafkaNativePartitionMover(admin.Object, Mock.Of<IKafkaBrokerGateway>(), Mock.Of<IKafkaThrottleConfig>(), 100);
        var error = await Assert.ThrowsAsync<KafkaOpsRejectedException>(() =>
            mover.SubmitAsync("{\"partitions\":[{\"topic\":\"t\",\"partition\":0,\"replicas\":[9]}]}", 0, 0, CancellationToken.None));
        Assert.Contains("t-0 INVALID_REPLICA_ASSIGNMENT broker 9 is not alive", error.Message, StringComparison.Ordinal);
    }
}
