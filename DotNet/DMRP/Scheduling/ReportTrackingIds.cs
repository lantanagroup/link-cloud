using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.DMRP.Scheduling
{
    /// <summary>
    /// A ReportTrackingId that is the same every time the same period is announced for the same
    /// facility. The Report service dead-letters a ReportScheduled whose id it has already stored,
    /// so a night that is re-fired - a misfire recovered, a pod restarted mid-loop, an operator
    /// triggering the job by hand - cannot create a second report for the same period.
    /// </summary>
    public static class ReportTrackingIds
    {
        // Fixed forever: changing it would make every id produced so far stop deduplicating.
        private static readonly Guid Namespace = new("6f1c2a4e-3b7d-4d3a-9c1e-2f0b5a8d7e61");

        public static Guid For(string facilityId, string frequency, DateTime periodStartUtc)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(facilityId);
            ArgumentException.ThrowIfNullOrWhiteSpace(frequency);

            return NameBasedGuid.CreateVersion5(Namespace, $"{facilityId}|{frequency}|{periodStartUtc:O}");
        }
    }
}
