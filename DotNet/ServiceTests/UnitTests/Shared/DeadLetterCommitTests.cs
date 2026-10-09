using System.Diagnostics.Metrics;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared;

[Trait("Category", "UnitTests")]
public class DeadLetterCommitTests
{
    [Fact]
    public async Task AccountAsync_Published_CommitsWithoutRewind()
    {
        var consumer = new Mock<IConsumer<string, string>>();
        var result = Result();

        var accounted = await DeadLetterCommit.AccountAsync(true, consumer.Object, result, Mock.Of<ILogger>(), CancellationToken.None);

        Assert.True(accounted);
        consumer.Verify(c => c.Seek(It.IsAny<TopicPartitionOffset>()), Times.Never);
    }

    [Fact]
    public async Task AccountAsync_PublishFailed_RewindsAndDoesNotCommit()
    {
        var consumer = new Mock<IConsumer<string, string>>();
        var result = Result();

        var accounted = await DeadLetterCommit.AccountAsync(false, consumer.Object, result, Mock.Of<ILogger>(), CancellationToken.None);

        Assert.False(accounted);
        consumer.Verify(c => c.Seek(result.TopicPartitionOffset), Times.Once);
    }

    [Fact]
    public async Task AccountAsync_SeekThrows_CountsTheFailureAndDoesNotCommit()
    {
        var consumer = new Mock<IConsumer<string, string>>();
        consumer.Setup(c => c.Seek(It.IsAny<TopicPartitionOffset>()))
            .Throws(new KafkaException(ErrorCode.Local_MsgTimedOut));
        var result = Result();
        long counted = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == DeadLetterCommit.MeterName
                    && instrument.Name == DeadLetterCommit.RewindFailedCounterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => counted += value);
        listener.Start();

        var accounted = await DeadLetterCommit.AccountAsync(false, consumer.Object, result, Mock.Of<ILogger>(), CancellationToken.None);

        Assert.False(accounted);
        Assert.Equal(1, counted);
        consumer.Verify(c => c.Seek(result.TopicPartitionOffset), Times.Once);
    }

    private static ConsumeResult<string, string> Result() =>
        new()
        {
            Topic = "PayloadSubmitted",
            Partition = new Partition(1),
            Offset = new Offset(4)
        };
}
