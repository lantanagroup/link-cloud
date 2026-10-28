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
    /// <para>
    /// A patient whose encounters all fell outside the reporting organization contributes nothing to the
    /// report: acquisition strips those encounters, and the measure evaluates no qualifying encounter for
    /// them. The column asks whether this patient's locations mapped <em>for this report</em>, so for a
    /// patient the report excludes there is no meaningful answer, and
    /// <see cref="MappingIndicatorStatus.Excluded"/> is it.
    /// </para>
    /// <para>
    /// That holds even when Normalization did report a result. Stripping the encounters does not
    /// necessarily empty the correlation -- the Location resources they referenced can survive and be code
    /// mapped perfectly well -- so Normalization can genuinely report <c>Mapped</c> for a patient who is
    /// not in the report at all. Surfacing that would describe a location the report never evaluates and
    /// read as a clean pass for an excluded patient.
    /// </para>
    /// </remarks>
    public static MappingIndicatorStatus ResolveHsloc(
        MappingIndicatorStatus stored,
        MappingIndicatorStatus locationOrgStatus)
    {
        // Unmapped here means acquisition found encounters and none belonged to the organization. The
        // patient is out of the report, so no code map result about them is worth reporting -- whether
        // Normalization answered or not. Any other value, including acquisition not having reported yet,
        // leaves the stored result to speak for itself.
        return locationOrgStatus == MappingIndicatorStatus.Unmapped
            ? MappingIndicatorStatus.Excluded
            : stored;
    }
}
