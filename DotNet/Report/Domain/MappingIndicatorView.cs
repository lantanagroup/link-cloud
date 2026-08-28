using LantanaGroup.Link.Report.Domain.Enums;

namespace LantanaGroup.Link.Report.Domain;

/// <summary>
/// Turns the stored mapping indicators into the ones the API reports.
/// </summary>
/// <remarks>
/// <para>
/// Storage records what each producer said about its own columns, and nothing more -- that separation is
/// what keeps two producers writing the same row from clobbering each other. One fact falls out of the
/// combination rather than either half: that a source is never going to report at all.
/// </para>
/// <para>
/// Resolving it here rather than on the write path keeps both properties. The stored row stays a faithful
/// record of what was reported, and the answer stays correct however the two messages interleave, because
/// it is computed from the final state of the row instead of from whichever message happened to arrive
/// second.
/// </para>
/// </remarks>
public static class MappingIndicatorView
{
    /// <summary>
    /// Resolves the HSLOC indicator to report, given the stored value and the acquisition columns.
    /// </summary>
    /// <remarks>
    /// Normalization reports nothing for a patient whose encounters all fell outside the reporting
    /// organization: acquisition strips them, so no resource ever reaches it and no message is ever
    /// produced. Stored, that is indistinguishable from a patient still in flight -- both are
    /// <see cref="MappingIndicatorStatus.NotEvaluated"/> with a null timestamp -- and reporting it that way
    /// leaves a row that never resolves and gives no reason why.
    /// </remarks>
    public static MappingIndicatorStatus ResolveHsloc(
        MappingIndicatorStatus stored,
        MappingIndicatorStatus locationOrgStatus,
        DateTime? normalizationEvaluatedAt)
    {
        // Normalization did report; its answer stands whatever the acquisition side says.
        if (normalizationEvaluatedAt is not null)
        {
            return stored;
        }

        // Acquisition found encounters and none of them belonged to the organization, which is the one
        // outcome that leaves nothing behind to normalize. Anything else -- including acquisition not
        // having reported yet -- is still genuinely pending.
        return locationOrgStatus == MappingIndicatorStatus.Unmapped
            ? MappingIndicatorStatus.Excluded
            : stored;
    }
}
