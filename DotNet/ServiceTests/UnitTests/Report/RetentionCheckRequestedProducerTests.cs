using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Shared.Application.Models.Retention;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

/// <summary>
/// Covers how a retention cleanup request goes onto RetentionCheckRequested: topic, key, correlation header,
/// cancellation and delivery failures.
/// </summary>
[Trait("Category", "UnitTests")]
public class RetentionCheckRequestedProducerTests
{
    private const string CleanupId = "5b0d7a9e-2c1f-4e3a-9f6b-1d2c3b4a5e6f";

    private readonly Mock<IProducer<string, RetentionCheckRequestedValue>> _producer = new();

    [Fact]
    public async Task PublishAsync_Request_ProducesItKeyedByCleanupId()
    {
        Message<string, RetentionCheckRequestedValue>? sent = null;
        string? topic = null;
        using var cancellation = new CancellationTokenSource();
        _producer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(),
                                       It.IsAny<Message<string, RetentionCheckRequestedValue>>(),
                                       cancellation.Token))
            .Callback((string t, Message<string, RetentionCheckRequestedValue> m, CancellationToken _) =>
            {
                topic = t;
                sent = m;
            })
            .ReturnsAsync(new DeliveryResult<string, RetentionCheckRequestedValue>());
        var request = Request();

        await new RetentionCheckRequestedProducer(_producer.Object).PublishAsync(request, cancellation.Token);

        Assert.Equal("RetentionCheckRequested", topic);
        Assert.NotNull(sent);
        Assert.Equal(CleanupId, sent.Key);
        Assert.Same(request, sent.Value);
        Assert.Equal(CleanupId, Encoding.UTF8.GetString(sent.Headers.GetLastBytes("X-Correlation-Id")));
    }

    [Fact]
    public async Task PublishAsync_DeliveryFails_Throws()
    {
        _producer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(),
                                       It.IsAny<Message<string, RetentionCheckRequestedValue>>(),
                                       It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProduceException<string, RetentionCheckRequestedValue>(
                new Error(ErrorCode.Local_MsgTimedOut),
                new DeliveryResult<string, RetentionCheckRequestedValue>()));

        var publisher = new RetentionCheckRequestedProducer(_producer.Object);

        await Assert.ThrowsAsync<ProduceException<string, RetentionCheckRequestedValue>>(
            () => publisher.PublishAsync(Request(), CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PublishAsync_NoCleanupId_ThrowsWithoutProducing(string cleanupId)
    {
        var request = Request();
        request.RetentionCleanupId = cleanupId;

        await Assert.ThrowsAsync<ArgumentException>(
            () => new RetentionCheckRequestedProducer(_producer.Object).PublishAsync(request, CancellationToken.None));

        _producer.VerifyNoOtherCalls();
    }

    private static RetentionCheckRequestedValue Request()
    {
        var cutoff = new DateTimeOffset(2026, 8, 6, 2, 0, 0, TimeSpan.Zero);

        return new RetentionCheckRequestedValue
        {
            RetentionCleanupId = CleanupId,
            InitiatedBy = RetentionInitiatedBy.Scheduled,
            InitiatedDateTime = cutoff.AddMonths(3),
            Mode = RetentionMode.Scrub,
            CutoffDateTime = cutoff,
            Reports =
            [
                new RetentionReport
                {
                    FacilityId = "facility-a",
                    ReportScheduleId = Guid.NewGuid().ToString(),
                    RetentionWindow = new RetentionWindow
                    {
                        Unit = RetentionWindowUnit.Months,
                        Count = 3
                    },
                    CutoffDateTime = cutoff
                }
            ]
        };
    }
}
