using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Shared.Application.Utilities
{
    /// <summary>
    /// Names the Quartz job group the classic per-facility <c>ReportScheduledJob</c>s live in.
    /// </summary>
    /// <remarks>
    /// Spelled once here because two assemblies need the same string: the Tenant service creates and
    /// deletes its per-facility jobs in this group, and the DMRP module deletes the whole group at boot
    /// when <c>DMRP:Enabled</c> is set and it takes scheduling over. The host passes this name into
    /// <c>AddDmrpModule</c> rather than the module knowing it, so the module stays independent of
    /// whichever host it is layered onto.
    /// </remarks>
    public static class ReportSchedulingJobs
    {
        /// <summary>The Quartz job group holding the classic per-facility report jobs.</summary>
        public const string ClassicJobGroup = nameof(KafkaTopic.ReportScheduled);
    }
}
