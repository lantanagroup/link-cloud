namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// Serializes configuration writes for a single facility.
//
// Configuration writes are read-modify-write against a record several steps share, and Link exposes
// no conditional write, so two requests that both read before either writes will silently drop one
// section. The BFF is the only layer able to serialize.
//
// Covers interleaving only. It does nothing for a stale tab saving sequentially with an older
// snapshot — the lock is uncontended in that case and grants instantly. That's handled separately by
// scoping the write to the step being saved.
public interface IFacilityWriteLock
{
    // Acquires the lock, waiting up to the configured timeout. The caller must call CommitAsync
    // once every guarded write has succeeded; disposing without committing rolls back.
    Task<IFacilityWriteLockHandle> AcquireAsync(string facilityId, CancellationToken cancellationToken = default);
}

public interface IFacilityWriteLockHandle : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

// The per-facility write lock could not be taken in time. Surfaces as 409 Conflict.
//
// Waiting briefly rather than failing immediately is deliberate: two browser tabs is the realistic
// contention case and a step write is one or two round trips, so the holder is usually gone in well
// under a second. An unbounded wait would convert contention into a hung request and a held database
// connection, since the lock is transaction-scoped.
public class FacilityWriteLockTimeoutException : Exception
{
    public FacilityWriteLockTimeoutException(string facilityId, int timeoutMs)
        : base($"Could not acquire the write lock for facility {facilityId} within {timeoutMs}ms. Another save is in progress.")
    {
        FacilityId = facilityId;
        TimeoutMs = timeoutMs;
    }

    public string FacilityId { get; }

    public int TimeoutMs { get; }
}
