namespace LantanaGroup.Link.Nhsn.App.Bff.Settings;

// The per-facility write lock.
public class FacilityWriteLockSettings
{
    public const string SectionName = "FacilityWriteLock";

    // How long a waiter will queue before giving up and returning 409.
    //
    // Diverges from the 30000ms the Data Acquisition usages employ, on purpose: those are batch
    // paths where nobody is waiting and a long queue is cheaper than a failure. This one is
    // interactive — a step write is two to four HTTP round trips, roughly 1-2s in normal
    // conditions, so 5s gives headroom before a waiter gives up while capping the worst case at
    // something a person tolerates.
    public int TimeoutMs { get; set; } = 5000;
}
