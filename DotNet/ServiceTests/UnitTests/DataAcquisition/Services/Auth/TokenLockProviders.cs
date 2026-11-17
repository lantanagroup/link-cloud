using Medallion.Threading;
using Moq;

namespace UnitTests.DataAcquisition.Auth;

/// <summary>
/// Distributed lock providers for the access token cache tests.
/// </summary>
internal static class TokenLockProviders
{
    /// <summary>
    /// Grants every lock at once without making callers wait for each other.
    /// </summary>
    public static IDistributedSemaphoreProvider Granting()
    {
        var handle = new Mock<IDistributedSynchronizationHandle>();

        return Build((_, _) => new ValueTask<IDistributedSynchronizationHandle?>(handle.Object));
    }

    /// <summary>
    /// Never grants a lock, as when the lock service is unreachable or the wait times out.
    /// </summary>
    public static IDistributedSemaphoreProvider Unavailable()
    {
        return Build((_, _) => new ValueTask<IDistributedSynchronizationHandle?>((IDistributedSynchronizationHandle?)null));
    }

    /// <summary>
    /// Grants one lock at a time; later callers wait until the holder disposes its handle.
    /// </summary>
    public static IDistributedSemaphoreProvider Serialized()
    {
        var gate = new SemaphoreSlim(1, 1);
        var handle = new Mock<IDistributedSynchronizationHandle>();
        handle
            .Setup(h => h.DisposeAsync())
            .Returns(() =>
            {
                gate.Release();
                return ValueTask.CompletedTask;
            });

        return Build((_, ct) => new ValueTask<IDistributedSynchronizationHandle?>(WaitForGateAsync(gate, handle.Object, ct)));
    }

    private static IDistributedSemaphoreProvider Build(
        Func<TimeSpan, CancellationToken, ValueTask<IDistributedSynchronizationHandle?>> tryAcquire)
    {
        var semaphore = new Mock<IDistributedSemaphore>();
        semaphore
            .Setup(s => s.TryAcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(tryAcquire);

        var provider = new Mock<IDistributedSemaphoreProvider>();
        provider
            .Setup(p => p.CreateSemaphore(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(semaphore.Object);

        return provider.Object;
    }

    private static async Task<IDistributedSynchronizationHandle?> WaitForGateAsync(SemaphoreSlim gate,
                                                                                   IDistributedSynchronizationHandle handle,
                                                                                   CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        return handle;
    }
}
