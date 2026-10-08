using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Extensions;
using Moq;

namespace UnitTests.Kafka;

[Trait("Category", "UnitTests")]
public class SafeCommitTests
{
    [Fact]
    public void SwallowsKafkaExceptionWhenThePartitionWasRevoked()
    {
        var consumer = new Mock<IConsumer<string, string>>();
        consumer.Setup(item => item.Commit()).Throws(new KafkaException(new Error(ErrorCode.UnknownMemberId, "revoked")));

        consumer.Object.SafeCommit();

        consumer.Verify(item => item.Commit(), Times.Once);
    }
}
