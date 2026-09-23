using System.Collections.Concurrent;

namespace Automation.UI.Services;

/// <summary>
/// DMRP runs share one NHSN organization id as the facility id. Facility setup deletes and
/// recreates that facility's normalization and location configuration, so two runs must not
/// do that at the same time.
/// </summary>
internal static class FacilitySetupGate
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<IDisposable> AcquireAsync(
        string facilityId,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var gate = Gates.GetOrAdd(facilityId, static _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            log($"Facility '{facilityId}' setup is already running. Waiting for that run to finish.");
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return new Release(gate);
    }

    private sealed class Release(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                gate.Release();
        }
    }
}
