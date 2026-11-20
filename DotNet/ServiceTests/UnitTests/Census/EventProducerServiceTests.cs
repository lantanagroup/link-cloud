using Confluent.Kafka;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Logging;
using Moq;
using PatientEvent = LantanaGroup.Link.Census.Application.Models.Messages.PatientEvent;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Census;

[Trait("Category", "UnitTests")]
public class EventProducerServiceTests
{
    [Theory]
    [InlineData("\r\n")]
    [InlineData("\u0085\u009b")]
    public async Task ProduceEventsAsync_SanitizesLogFields_WithoutChangingKafkaData(string controls)
    {
        var logger = new CapturingLogger();
        var producer = new Mock<IProducer<string, PatientEvent>>();
        using var cancellation = new CancellationTokenSource();
        var response = CreateResponse(controls);
        Message<string, PatientEvent>? sent = null;
        producer.Setup(p => p.ProduceAsync(KafkaTopic.PatientEvent.ToString(),
                It.IsAny<Message<string, PatientEvent>>(), cancellation.Token))
            .Callback<string, Message<string, PatientEvent>, CancellationToken>((_, message, _) => sent = message)
            .ReturnsAsync(new DeliveryResult<string, PatientEvent>());
        var service = new EventProducerService<PatientEvent>(logger, producer.Object);

        await service.ProduceEventsAsync(response.FacilityId, [response], cancellation.Token);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.DoesNotContain(entry.Message, char.IsControl);
        Assert.Contains("PatientId = patient  forged", entry.Message);
        Assert.NotNull(sent);
        Assert.Equal(response.FacilityId, sent.Key);
        Assert.Same(response.PatientEvent, sent.Value);
        Assert.Equal("patient" + controls + "forged", sent.Value.PatientId);
        producer.VerifyAll();
    }

    [Fact]
    public async Task ProduceEventsAsync_SanitizesFacilityInFailureLog_AndRethrows()
    {
        var logger = new CapturingLogger();
        var producer = new Mock<IProducer<string, PatientEvent>>();
        var failure = new ProduceException<string, PatientEvent>(
            new Error(ErrorCode.Local_Transport), new DeliveryResult<string, PatientEvent>());
        producer.Setup(p => p.ProduceAsync(It.IsAny<string>(),
                It.IsAny<Message<string, PatientEvent>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var response = CreateResponse();
        var service = new EventProducerService<PatientEvent>(logger, producer.Object);

        var thrown = await Assert.ThrowsAsync<ProduceException<string, PatientEvent>>(
            () => service.ProduceEventsAsync(response.FacilityId, [response]));

        Assert.Same(failure, thrown);
        var entry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.DoesNotContain(entry.Message, char.IsControl);
        Assert.Contains("facility  forged", entry.Message);
    }

    [Fact]
    public async Task ProduceEventsAsync_ObservesCancellation_WhenEventsWouldBeSkipped()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var producer = new Mock<IProducer<string, PatientEvent>>(MockBehavior.Strict);
        var response = CreateResponse();
        response.PatientEvent!.EventType = "Update";
        var service = new EventProducerService<PatientEvent>(new CapturingLogger(), producer.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ProduceEventsAsync(response.FacilityId, [response], cancellation.Token));

        producer.VerifyNoOtherCalls();
    }

    private static PatientEventResponse CreateResponse(string controls = "\r\n") => new()
    {
        FacilityId = "facility" + controls + "forged",
        CorrelationId = Guid.NewGuid().ToString(),
        PatientEvent = new PatientEvent { PatientId = "patient" + controls + "forged", EventType = "Admit" }
    };

    private sealed class CapturingLogger : ILogger<EventProducerService<PatientEvent>>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }
}
