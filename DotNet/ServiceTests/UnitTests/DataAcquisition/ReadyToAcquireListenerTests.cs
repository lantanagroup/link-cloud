using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Mapping;
using LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Listeners;
using LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Internal;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using DaRequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class ReadyToAcquireListenerTests
{
    [Fact]
    public async Task ExecuteListenerAsync_WhenNotClaimed_ReturnsWithoutEnqueue()
    {
        var logManagerMock = new Mock<IDataAcquisitionLogManager>();
        logManagerMock
            .Setup(m => m.TrySetLogToQueuedAsync(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var processor = new TestAcquisitionProcessorBackgroundService
        {
            OnEnqueueAsync = (_, _) => ValueTask.CompletedTask
        };

        var listener = CreateListener(logManagerMock, processor);

        await listener.InvokeExecuteListenerAsync(CreateConsumeResult(123, "facility-a"), CancellationToken.None);

        Assert.Equal(0, processor.EnqueueCallCount);
        logManagerMock.Verify(m => m.TrySetLogToQueuedAsync(123, It.IsAny<CancellationToken>()), Times.Once);
        logManagerMock.Verify(m => m.TrySetLogStatusAsync(It.IsAny<long>(), It.IsAny<List<DaRequestStatus>>(), It.IsAny<DaRequestStatus>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteListenerAsync_WhenEnqueueCanceled_PropagatesCancellationWithoutCompensation()
    {
        var logManagerMock = new Mock<IDataAcquisitionLogManager>();
        logManagerMock
            .Setup(m => m.TrySetLogToQueuedAsync(456, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var processor = new TestAcquisitionProcessorBackgroundService
        {
            OnEnqueueAsync = (_, _) => throw new OperationCanceledException("shutdown")
        };

        var listener = CreateListener(logManagerMock, processor);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            listener.InvokeExecuteListenerAsync(CreateConsumeResult(456, "facility-b"), CancellationToken.None));

        logManagerMock.Verify(m => m.TrySetLogStatusAsync(It.IsAny<long>(), It.IsAny<List<DaRequestStatus>>(), It.IsAny<DaRequestStatus>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteListenerAsync_WhenEnqueueFailsAndCompensationSucceeds_ReturnsWithoutThrowing()
    {
        var logManagerMock = new Mock<IDataAcquisitionLogManager>();
        logManagerMock
            .Setup(m => m.TrySetLogToQueuedAsync(789, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        logManagerMock
            .Setup(m => m.TrySetLogStatusAsync(
                789,
                It.Is<List<DaRequestStatus>>(s => s.Count == 1 && s[0] == DaRequestStatus.Queued),
                DaRequestStatus.Pending,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var processor = new TestAcquisitionProcessorBackgroundService
        {
            OnEnqueueAsync = (_, _) => throw new Exception("enqueue failed")
        };

        var listener = CreateListener(logManagerMock, processor);

        // After successful compensation the listener must NOT throw TransientException, otherwise
        // the message would be re-published to the -Retry topic and cause an infinite backpressure
        // loop (see LNK-5129). The log is already reverted to Pending and will be re-triggered by
        // the scheduled acquisition job.
        await listener.InvokeExecuteListenerAsync(CreateConsumeResult(789, "facility-c"), CancellationToken.None);

        logManagerMock.Verify(m => m.TrySetLogStatusAsync(
            789,
            It.Is<List<DaRequestStatus>>(s => s.Count == 1 && s[0] == DaRequestStatus.Queued),
            DaRequestStatus.Pending,
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteListenerAsync_WhenEnqueueFailsAndCompensationFails_ThrowsDeadLetterException()
    {
        var logManagerMock = new Mock<IDataAcquisitionLogManager>();
        logManagerMock
            .Setup(m => m.TrySetLogToQueuedAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        logManagerMock
            .Setup(m => m.TrySetLogStatusAsync(
                999,
                It.Is<List<DaRequestStatus>>(s => s.Count == 1 && s[0] == DaRequestStatus.Queued),
                DaRequestStatus.Pending,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var processor = new TestAcquisitionProcessorBackgroundService
        {
            OnEnqueueAsync = (_, _) => throw new Exception("enqueue failed")
        };

        var listener = CreateListener(logManagerMock, processor);

        await Assert.ThrowsAsync<DeadLetterException>(() =>
            listener.InvokeExecuteListenerAsync(CreateConsumeResult(999, "facility-d"), CancellationToken.None));

        logManagerMock.Verify(m => m.TrySetLogStatusAsync(
            999,
            It.Is<List<DaRequestStatus>>(s => s.Count == 1 && s[0] == DaRequestStatus.Queued),
            DaRequestStatus.Pending,
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static TestReadyToAcquireListener CreateListener(
        Mock<IDataAcquisitionLogManager> logManagerMock,
        TestAcquisitionProcessorBackgroundService processor)
    {
        var deadLetterHandlerMock = new Mock<IDeadLetterExceptionHandler<ReadyToAcquire, long, ReadyToAcquire>>();
        var deadLetterErrorHandlerMock = new Mock<IDeadLetterExceptionHandler<ReadyToAcquire, string, string>>();
        var transientHandlerMock = new Mock<ITransientExceptionHandler<ReadyToAcquire, long, ReadyToAcquire>>();
        var consumerFactoryMock = new Mock<IKafkaConsumerFactory<long, ReadyToAcquire>>();

        var services = new ServiceCollection();
        services.AddScoped(_ => logManagerMock.Object);
        services.AddScoped<AcquisitionProcessorBackgroundService>(_ => processor);
        var provider = services.BuildServiceProvider();

        return new TestReadyToAcquireListener(
            new Mock<ILogger<ReadyToAcquireListener>>().Object,
            consumerFactoryMock.Object,
            deadLetterHandlerMock.Object,
            deadLetterErrorHandlerMock.Object,
            transientHandlerMock.Object,
            new ServiceInformation { ServiceConfigName = "unit-tests" },
            provider.GetRequiredService<IServiceScopeFactory>());
    }

    private static ConsumeResult<long, ReadyToAcquire> CreateConsumeResult(long logId, string facilityId)
    {
        return new ConsumeResult<long, ReadyToAcquire>
        {
            Message = new Message<long, ReadyToAcquire>
            {
                Key = logId,
                Value = new ReadyToAcquire
                {
                    LogId = logId,
                    FacilityId = facilityId,
                    ReportTrackingId = Guid.NewGuid().ToString()
                }
            }
        };
    }

    private sealed class TestReadyToAcquireListener : ReadyToAcquireListener
    {
        public TestReadyToAcquireListener(
            ILogger<ReadyToAcquireListener> logger,
            IKafkaConsumerFactory<long, ReadyToAcquire> kafkaConsumerFactory,
            IDeadLetterExceptionHandler<ReadyToAcquire, long, ReadyToAcquire> deadLetterConsumerHandler,
            IDeadLetterExceptionHandler<ReadyToAcquire, string, string> deadLetterConsumerErrorHandler,
            ITransientExceptionHandler<ReadyToAcquire, long, ReadyToAcquire> transientExceptionHandler,
            ServiceInformation serviceInformation,
            IServiceScopeFactory serviceScopeFactory)
            : base(logger, kafkaConsumerFactory, deadLetterConsumerHandler, deadLetterConsumerErrorHandler, transientExceptionHandler, serviceInformation, serviceScopeFactory)
        {
        }

        public Task InvokeExecuteListenerAsync(ConsumeResult<long, ReadyToAcquire> consumeResult, CancellationToken cancellationToken)
            => ExecuteListenerAsync(consumeResult, cancellationToken);
    }

    private sealed class TestAcquisitionProcessorBackgroundService : AcquisitionProcessorBackgroundService
    {
        public TestAcquisitionProcessorBackgroundService()
            : base(
                new Mock<ILogger<AcquisitionProcessorBackgroundService>>().Object,
                new ServiceCollection().BuildServiceProvider(),
                new Mock<IProducer<ResourceKey, ResourcesAcquired>>().Object,
                new Mock<IProducer<ResourceKey, MappingOutcomeEvaluatedValue>>().Object,
                Options.Create(new LantanaGroup.Link.DataAcquisition.Domain.Settings.AcquisitionWorkerProcessorSettings()))
        {
        }

        public Func<AcquisitionWorkItem, CancellationToken, ValueTask>? OnEnqueueAsync { get; set; }
        public int EnqueueCallCount { get; private set; }

        public override async ValueTask EnqueueAsync(AcquisitionWorkItem item, CancellationToken ct = default)
        {
            EnqueueCallCount++;
            if (OnEnqueueAsync != null)
            {
                await OnEnqueueAsync(item, ct);
            }
        }
    }
}
