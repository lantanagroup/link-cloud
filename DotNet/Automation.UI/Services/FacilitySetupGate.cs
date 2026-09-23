using System.Collections.Concurrent;

namespace Automation.UI.Services;

/// <summary>
/// DMRP runs share one NHSN organization id as the facility id. Facility setup deletes and
/// recreates that facility's normalization and location configuration, so two runs must not
/// do that at the same time.
/// </summary>
internal static class FacilitySetupGate
{
    private static readonly ConcurrentDictionary<string, Entry> Gates = new(StringComparer.OrdinalIgnoreCase);

    internal static bool IsTracking(string facilityId) => Gates.ContainsKey(facilityId);

    public static async Task<IDisposable> AcquireAsync(
        string facilityId,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var entry = Gates.GetOrAdd(facilityId, static _ => new Entry());
            var counted = false;
            lock (entry)
            {
                if (Gates.TryGetValue(facilityId, out var current) && ReferenceEquals(current, entry))
                {
                    entry.Refs++;
                    counted = true;
                }
            }

            if (!counted)
                continue;

            var acquired = false;
            try
            {
                if (!await entry.Gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
                {
                    log($"Facility '{facilityId}' setup is already running. Waiting for that run to finish.");
                    await entry.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                acquired = true;
                return new Release(facilityId, entry);
            }
            catch
            {
                if (!acquired)
                    ReleaseRef(facilityId, entry);
                throw;
            }
        }
    }

    private static void ReleaseRef(string facilityId, Entry entry)
    {
        lock (entry)
        {
            entry.Refs--;
            if (entry.Refs != 0)
                return;

            if (Gates.TryRemove(new KeyValuePair<string, Entry>(facilityId, entry)))
                entry.Gate.Dispose();
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public int Refs;
    }

    private sealed class Release(string facilityId, Entry entry) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
                return;

            entry.Gate.Release();
            ReleaseRef(facilityId, entry);
        }
    }
}
