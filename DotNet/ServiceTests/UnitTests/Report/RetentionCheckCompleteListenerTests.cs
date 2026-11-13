using System.Reflection;
using Confluent.Kafka;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UnitTests.Shared.Auditing;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

/// <summary>
/// Covers what the listener checks before handing a completion to the manager, and where its error handlers
/// send a failed message.
/// </summary>
[Trait("Category", "UnitTests")]
public class RetentionCheckCompleteListenerTests
{
    private static readonly Guid CleanupId = Guid.NewGuid();

    private readonly Mock<IRetentionCleanupManager> _manager = new();
    private readonly Mock<IDeadLetterExceptionHandler<RetentionCheckCompleteListener, string, string>>
        _consumeHandler = new();
    private readonly Mock<IDeadLetterExceptionHandler<RetentionCheckCompleteListener, string, RetentionCheckCompleteValue>>
        _deadLetterHandler = new();
    private readonly Mock<ITransientExceptionHandler<RetentionCheckCompleteListener, string, RetentionCheckCompleteValue>>
        _transientHandler = new();
    private readonly RetentionCheckCompleteListener _listener;

    public RetentionCheckCompleteListenerTests()
    {
        // SetupProperty keeps the constructor's Topic assignments, so the wiring test is not vacuous.
        _consumeHandler.SetupProperty(item => item.Topic);
        _deadLetterHandler.SetupProperty(item => item.Topic);
        _transientHandler.SetupProperty(item => item.Topic);

        var services = new ServiceCollection();
        services.AddSingleton(_manager.Object);
        var serviceProvider = services.BuildServiceProvider();

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(item => item.ServiceProvider).Returns(serviceProvider);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(item => item.CreateScope()).Returns(scope.Object);

        _listener = new RetentionCheckCompleteListener(
            Mock.Of<ILogger<RetentionCheckCompleteListener>>(),
            Mock.Of<IKafkaConsumerFactory<string, RetentionCheckCompleteValue>>(),
            new ServiceInformation { ServiceConfigName = "Report" },
            _consumeHandler.Object,
            _deadLetterHandler.Object,
            _transientHandler.Object,
            Mock.Of<IExceptionLogger<RetentionCheckCompleteListener>>(),
            scopeFactory.Object);
    }

    [Fact]
    public void Constructor_Always_WiresTheErrorAndRetryTopics()
    {
        Assert.Equal("RetentionCheckComplete-Error", _consumeHandler.Object.Topic);
        Assert.Equal("RetentionCheckComplete-Error", _deadLetterHandler.Object.Topic);
        Assert.Equal("RetentionCheckComplete-Retry", _transientHandler.Object.Topic);
    }

    [Fact]
    public async Task ConsumeMessageAsync_ValidCompletion_AppliesItToTheCleanup()
    {
        var completion = Completion(CleanupId.ToString(), "MeasureEval");
        _manager
            .Setup(item => item.ApplyCompletionAsync(CleanupId, completion, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RetentionCompletionOutcome.Applied);

        await ConsumeAsync(Message(CleanupId.ToString(), completion));

        _manager.Verify(item => item.ApplyCompletionAsync(CleanupId, completion, It.IsAny<CancellationToken>()),
                        Times.Once);
    }

    [Fact]
    public async Task ConsumeMessageAsync_KeyDiffersFromTheValue_UsesTheValue()
    {
        var completion = Completion(CleanupId.ToString(), "Validation");

        await ConsumeAsync(Message(Guid.NewGuid().ToString(), completion));

        _manager.Verify(item => item.ApplyCompletionAsync(CleanupId, completion, It.IsAny<CancellationToken>()),
                        Times.Once);
    }

    [Theory]
    [InlineData("not-a-uuid", "MeasureEval")]
    [InlineData("", "MeasureEval")]
    [InlineData("00000000-0000-0000-0000-000000000001", " ")]
    public async Task ConsumeMessageAsync_MalformedCompletion_ThrowsDeadLetter(string retentionCleanupId,
                                                                              string serviceName)
    {
        var completion = Completion(retentionCleanupId, serviceName);

        await Assert.ThrowsAsync<DeadLetterException>(() => ConsumeAsync(Message(retentionCleanupId, completion)));

        _manager.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConsumeMessageAsync_UndefinedStatus_ThrowsDeadLetter()
    {
        var completion = Completion(CleanupId.ToString(), "MeasureEval");
        completion.Reports =
        [
            new RetentionReportResult
            {
                ReportScheduleId = Guid.NewGuid().ToString(),
                Status = (RetentionResultStatus)2,
                RecordsAffected = 0,
                CompletedDateTime = completion.CompletedDateTime
            }
        ];

        await Assert.ThrowsAsync<DeadLetterException>(() => ConsumeAsync(Message(CleanupId.ToString(), completion)));

        _manager.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConsumeMessageAsync_NullValue_ThrowsDeadLetter()
    {
        await Assert.ThrowsAsync<DeadLetterException>(() => ConsumeAsync(Message(CleanupId.ToString(), null)));

        _manager.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StartConsumerLoop_TopicMissing_LogsOnceAndKeepsPolling()
    {
        using var cancellation = new CancellationTokenSource();
        var missingTopic = new ConsumeException(new ConsumeResult<byte[], byte[]>(),
                                                new Error(ErrorCode.UnknownTopicOrPart));
        var consumer = new Mock<IConsumer<string, RetentionCheckCompleteValue>>();
        var calls = 0;
        consumer
            .Setup(item => item.Consume(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(_ =>
            {
                calls++;
                if (calls <= 2)
                {
                    throw missingTopic;
                }

                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            });
        var logger = new CapturingLogger<RetentionCheckCompleteListener>();
        var listener = CreateListener(consumer.Object, logger);

        var exception = await Record.ExceptionAsync(() => RunConsumerLoopAsync(listener, cancellation.Token));

        Assert.Null(exception);
        Assert.Equal(3, calls);
        var error = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Contains("does not exist yet", error.Message);
        Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Debug);
        _consumeHandler.Verify(item => item.HandleConsumeException(It.IsAny<ConsumeException>(), It.IsAny<string>()),
                               Times.Never);
    }

    private RetentionCheckCompleteListener CreateListener(IConsumer<string, RetentionCheckCompleteValue> consumer,
                                                          ILogger<RetentionCheckCompleteListener> logger)
    {
        var consumerFactory = new Mock<IKafkaConsumerFactory<string, RetentionCheckCompleteValue>>();
        consumerFactory
            .Setup(item => item.CreateConsumer(It.IsAny<ConsumerConfig>(),
                                               It.IsAny<IDeserializer<string>>(),
                                               It.IsAny<IDeserializer<RetentionCheckCompleteValue>>(),
                                               It.IsAny<KafkaAssignmentTracker?>()))
            .Returns(consumer);

        return new RetentionCheckCompleteListener(
            logger,
            consumerFactory.Object,
            new ServiceInformation { ServiceConfigName = "Report" },
            _consumeHandler.Object,
            _deadLetterHandler.Object,
            _transientHandler.Object,
            Mock.Of<IExceptionLogger<RetentionCheckCompleteListener>>(),
            Mock.Of<IServiceScopeFactory>())
        {
            MissingTopicRetryDelay = TimeSpan.Zero
        };
    }

    private static async Task RunConsumerLoopAsync(RetentionCheckCompleteListener listener,
                                                   CancellationToken cancellationToken)
    {
        var method = typeof(RetentionCheckCompleteListener)
            .GetMethod("StartConsumerLoop", BindingFlags.NonPublic | BindingFlags.Instance)!;

        try
        {
            await (Task)method.Invoke(listener, [cancellationToken])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static RetentionCheckCompleteValue Completion(string retentionCleanupId, string serviceName)
    {
        return new RetentionCheckCompleteValue
        {
            RetentionCleanupId = retentionCleanupId,
            ServiceName = serviceName,
            CompletedDateTime = new DateTimeOffset(2026, 11, 5, 2, 0, 0, TimeSpan.Zero),
            Reports = []
        };
    }

    private static ConsumeResult<string, RetentionCheckCompleteValue> Message(string key,
                                                                             RetentionCheckCompleteValue? value)
    {
        return new ConsumeResult<string, RetentionCheckCompleteValue>
        {
            Topic = "RetentionCheckComplete",
            Partition = new Partition(0),
            Offset = new Offset(0),
            Message = new Message<string, RetentionCheckCompleteValue> { Key = key, Value = value! }
        };
    }

    private async Task ConsumeAsync(ConsumeResult<string, RetentionCheckCompleteValue> message)
    {
        var method = typeof(RetentionCheckCompleteListener)
            .GetMethod("ConsumeMessageAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        try
        {
            await (Task)method.Invoke(_listener, [message, CancellationToken.None])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}
